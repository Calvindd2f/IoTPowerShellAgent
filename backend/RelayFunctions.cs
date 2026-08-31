// =============================================================================
// IoTPowerShellAgent.Relay
//
// POST /api/jobs           workflow -> relay. Encodes the script, invokes
//                          ExecuteScript on the device, records the job.
// POST /api/results        agent -> relay. Validates the job token, forwards
//                          the result to the job's callbackUrl, closes the job.
// GET  /api/jobs/{jobId}   workflow -> relay. Poll fallback for tools that
//                          can't hold a webhook open.
//
// The agent is never told a workflow-supplied URL. It only ever knows this
// relay's own hostname (baked in at install time, same as IOTHUB_HOSTNAME).
// callbackUrl lives in the jobs table, validated against ALLOWED_CALLBACK_HOSTS
// at dispatch time - a compromised dispatcher credential can redirect a job's
// output to another allowlisted host, never to an arbitrary one.
// =============================================================================

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IoTPowerShellAgent.Relay;

// ---------------------------------------------------------------------------
// Contracts
// ---------------------------------------------------------------------------

public sealed record DispatchRequest(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("script")] string Script,
    [property: JsonPropertyName("callbackUrl")] string CallbackUrl,
    [property: JsonPropertyName("isBase64")] bool IsBase64 = false,
    [property: JsonPropertyName("timeoutSeconds")] int TimeoutSeconds = 300,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null);

/// <summary>Payload delivered to the agent via the ExecuteScript direct method.</summary>
public sealed record ExecuteScriptPayload(
    [property: JsonPropertyName("JobId")] string JobId,
    [property: JsonPropertyName("Script")] string Script,
    [property: JsonPropertyName("IsInlinePowershell")] bool IsInlinePowershell,
    [property: JsonPropertyName("ResultEndpoint")] string ResultEndpoint,
    [property: JsonPropertyName("JobToken")] string JobToken,
    [property: JsonPropertyName("TimeoutSeconds")] int TimeoutSeconds,
    [property: JsonPropertyName("Detached")] bool Detached);

public sealed class JobEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";   // deviceId
    public string RowKey { get; set; } = "";          // jobId
    public string Status { get; set; } = "dispatched"; // dispatched | completed | expired | error
    public string CallbackUrl { get; set; } = "";
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public bool? Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ForwardStatus { get; set; }         // ok | failed | not-configured

    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}

// ---------------------------------------------------------------------------

public sealed class RelayFunctions
{
    private const string ExecuteScriptMethod = "ExecuteScript";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger<RelayFunctions> _log;
    private readonly ServiceClient _iot;
    private readonly TableClient _jobs;
    private readonly IHttpClientFactory _http;

    private readonly int _jobTtlMinutes = int.Parse(Env("JOB_TTL_MINUTES", "20"));
    private readonly string _signingSecret = Env("AGENT_SIGNING_SECRET");
    private readonly string _publicHost = Env("WEBSITE_HOSTNAME", "localhost");
    private readonly string[] _allowedCallbackHosts =
        Env("ALLOWED_CALLBACK_HOSTS", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public RelayFunctions(ILogger<RelayFunctions> log, IHttpClientFactory http)
    {
        _log = log;
        _http = http;

        var cred = new DefaultAzureCredential();
        _iot = ServiceClient.Create(Env("IOTHUB_HOSTNAME"), cred, TransportType.Amqp);
        _jobs = new TableClient(new Uri(Env("JOBS_TABLE_ENDPOINT")), Env("JOBS_TABLE_NAME"), cred);
    }

    // -----------------------------------------------------------------------
    // POST /api/jobs
    // -----------------------------------------------------------------------

    [Function("DispatchJob")]
    public async Task<HttpResponseData> DispatchJob(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await JsonSerializer.DeserializeAsync<DispatchRequest>(req.Body, Json, ct);
        if (body is null || string.IsNullOrWhiteSpace(body.DeviceId) || string.IsNullOrWhiteSpace(body.Script))
            return await Problem(req, HttpStatusCode.BadRequest, "deviceId and script are required.");

        if (!IsAllowedCallbackHost(body.CallbackUrl, out var reason))
            return await Problem(req, HttpStatusCode.BadRequest, $"callbackUrl rejected: {reason}");

        var jobId = Guid.NewGuid().ToString("n");
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_jobTtlMinutes);

        var entity = new JobEntity
        {
            PartitionKey = body.DeviceId,
            RowKey = jobId,
            Status = "dispatched",
            CallbackUrl = body.CallbackUrl,
            CorrelationId = body.CorrelationId,
            CreatedUtc = now,
            ExpiresUtc = expires
        };
        await _jobs.AddEntityAsync(entity, ct);

        var payload = new ExecuteScriptPayload(
            JobId: jobId,
            // Script travels exactly as given: if the caller already base64-encoded
            // it, IsInlinePowershell reflects that rather than double-encoding here.
            Script: body.Script,
            IsInlinePowershell: !body.IsBase64,
            ResultEndpoint: $"https://{_publicHost}/api/results",
            JobToken: MintJobToken(body.DeviceId, jobId, expires),
            TimeoutSeconds: body.TimeoutSeconds,
            // Direct methods hard-cap at 300s and their responses at 8 KB - far
            // too small for a real result set. Detached: the agent acks in
            // milliseconds and runs the script on a background thread, POSTing
            // the result to ResultEndpoint when it finishes.
            Detached: true);

        var method = new CloudToDeviceMethod(ExecuteScriptMethod)
        {
            ResponseTimeout = TimeSpan.FromSeconds(30),
            ConnectionTimeout = TimeSpan.FromSeconds(30)
        };
        method.SetPayloadJson(JsonSerializer.Serialize(payload, Json));

        CloudToDeviceMethodResult ack;
        try
        {
            ack = await _iot.InvokeDeviceMethodAsync(body.DeviceId, method, ct);
        }
        catch (Exception ex)
        {
            entity.Status = "error";
            entity.ErrorMessage = ex.Message;
            await _jobs.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);

            _log.LogError(ex, "Dispatch failed for {DeviceId} job {JobId}", body.DeviceId, jobId);
            // 404/DeviceNotOnline is the common case: direct methods do not queue.
            return await Problem(req, HttpStatusCode.BadGateway, $"Device unreachable: {ex.Message}");
        }

        _log.LogInformation("Dispatched job {JobId} to {DeviceId}, ack status {Status}", jobId, body.DeviceId, ack.Status);

        var res = req.CreateResponse(HttpStatusCode.Accepted);
        await res.WriteAsJsonAsync(new
        {
            jobId,
            deviceId = body.DeviceId,
            status = "dispatched",
            expiresUtc = expires,
            deviceAckStatus = ack.Status,
            pollUrl = $"https://{_publicHost}/api/jobs/{jobId}?deviceId={Uri.EscapeDataString(body.DeviceId)}"
        }, ct);
        return res;
    }

    // -----------------------------------------------------------------------
    // POST /api/results  (agent-facing)
    // -----------------------------------------------------------------------

    [Function("PostResult")]
    public async Task<HttpResponseData> PostResult(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "results")] HttpRequestData req,
        CancellationToken ct)
    {
        if (!req.Headers.TryGetValues("x-job-token", out var tokens))
            return await Problem(req, HttpStatusCode.Unauthorized, "Missing x-job-token.");

        using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
        var root = doc.RootElement;

        var jobId = root.TryGetProperty("jobId", out var j) ? j.GetString() : null;
        var deviceId = root.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
        if (jobId is null || deviceId is null)
            return await Problem(req, HttpStatusCode.BadRequest, "jobId and deviceId are required in the envelope.");

        if (!TryValidateJobToken(tokens.First(), deviceId, jobId, out _))
            return await Problem(req, HttpStatusCode.Forbidden, "Invalid or expired job token.");

        JobEntity entity;
        try
        {
            entity = await _jobs.GetEntityAsync<JobEntity>(deviceId, jobId, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return await Problem(req, HttpStatusCode.NotFound, "Unknown job.");
        }

        if (entity.Status != "dispatched")
        {
            // Already completed, expired, or errored - PostResult is not retried
            // server-side. Log and 200 so the agent doesn't hammer a dead job.
            _log.LogWarning("Result for job {JobId} arrived in state {Status}; ignoring.", jobId, entity.Status);
            var stale = req.CreateResponse(HttpStatusCode.OK);
            await stale.WriteAsJsonAsync(new { jobId, accepted = false, reason = entity.Status }, ct);
            return stale;
        }

        if (DateTimeOffset.UtcNow > entity.ExpiresUtc)
        {
            entity.Status = "expired";
            await _jobs.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
            return await Problem(req, HttpStatusCode.Gone, "Job expired before the result arrived.");
        }

        var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
        var errorMessage = root.TryGetProperty("errorMessage", out var em) ? em.GetString() : null;

        var forwardStatus = await ForwardToWorkflowAsync(entity.CallbackUrl, doc.RootElement.GetRawText(), ct);

        entity.Status = "completed";
        entity.Success = success;
        entity.ErrorMessage = errorMessage;
        entity.ForwardStatus = forwardStatus;
        await _jobs.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);

        _log.LogInformation("Job {JobId} completed (success={Success}), forward={ForwardStatus}", jobId, success, forwardStatus);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new { jobId, accepted = true, forwardStatus }, ct);
        return res;
    }

    // -----------------------------------------------------------------------
    // GET /api/jobs/{jobId}
    // -----------------------------------------------------------------------

    [Function("GetJob")]
    public async Task<HttpResponseData> GetJob(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "jobs/{jobId}")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var deviceId = query["deviceId"];
        if (string.IsNullOrEmpty(deviceId))
            return await Problem(req, HttpStatusCode.BadRequest, "deviceId query parameter is required (jobs are partitioned by device).");

        try
        {
            var entity = await _jobs.GetEntityAsync<JobEntity>(deviceId, jobId, cancellationToken: ct);
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new
            {
                jobId = entity.Value.RowKey,
                deviceId = entity.Value.PartitionKey,
                status = entity.Value.Status,
                success = entity.Value.Success,
                errorMessage = entity.Value.ErrorMessage,
                createdUtc = entity.Value.CreatedUtc,
                expiresUtc = entity.Value.ExpiresUtc,
                forwardStatus = entity.Value.ForwardStatus
            }, ct);
            return res;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return await Problem(req, HttpStatusCode.NotFound, "Unknown job.");
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private bool IsAllowedCallbackHost(string? callbackUrl, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(callbackUrl)) { reason = "missing"; return false; }
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        {
            reason = "must be an absolute https URL";
            return false;
        }
        if (_allowedCallbackHosts.Length == 0) return true; // explicitly opted out at deploy time

        foreach (var suffix in _allowedCallbackHosts)
        {
            if (uri.Host.Equals(suffix.TrimStart('.'), StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        reason = $"host '{uri.Host}' is not on ALLOWED_CALLBACK_HOSTS";
        return false;
    }

    private async Task<string> ForwardToWorkflowAsync(string callbackUrl, string envelopeJson, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient("workflow");
            using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
            {
                Content = new StringContent(envelopeJson, Encoding.UTF8, "application/json")
            };

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
            var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(envelopeJson))).ToLowerInvariant();
            request.Headers.TryAddWithoutValidation("X-Agent-Signature", $"sha256={sig}");

            var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode ? "ok" : $"failed ({(int)response.StatusCode})";
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Forward to workflow callback failed.");
            return "failed (exception)";
        }
    }

    private string MintJobToken(string deviceId, string jobId, DateTimeOffset expires)
    {
        var exp = expires.ToUnixTimeSeconds();
        var payload = $"{deviceId}|{jobId}|{exp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return $"{exp}.{sig}";
    }

    private bool TryValidateJobToken(string token, string deviceId, string jobId, out DateTimeOffset expiry)
    {
        expiry = default;
        var split = token.Split('.', 2);
        if (split.Length != 2 || !long.TryParse(split[0], out var exp)) return false;

        expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
        if (expiry <= DateTimeOffset.UtcNow) return false;

        var payload = $"{deviceId}|{jobId}|{exp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        byte[] actual;
        try { actual = Convert.FromBase64String(split[1]); } catch { return false; }

        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static async Task<HttpResponseData> Problem(HttpRequestData req, HttpStatusCode code, string detail)
    {
        var res = req.CreateResponse(code);
        await res.WriteAsJsonAsync(new { status = (int)code, detail });
        return res;
    }

    private static string Env(string name, string? fallback = null)
        => Environment.GetEnvironmentVariable(name)
           ?? fallback
           ?? throw new InvalidOperationException($"App setting '{name}' is not configured.");
}
