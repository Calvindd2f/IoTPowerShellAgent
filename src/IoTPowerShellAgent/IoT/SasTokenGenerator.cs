using System;
using System.Security.Cryptography;
using System.Text;

namespace IoTPowerShellAgent.IoT
{
    /// <summary>
    /// Generates SAS tokens for Azure IoT Hub authentication
    /// </summary>
    public static class SasTokenGenerator
    {
        /// <summary>
        /// Generates a SAS token for Azure IoT Hub
        /// </summary>
        /// <param name="resourceUri">Resource URI (e.g., "your-iothub.azure-devices.net/devices/device-id")</param>
        /// <param name="sharedAccessKey">Shared access key (base64 encoded)</param>
        /// <param name="duration">Token validity duration</param>
        /// <returns>SAS token string</returns>
        public static string GenerateSasToken(string resourceUri, string sharedAccessKey, TimeSpan duration)
        {
            // Set expiration time
            long expiration = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds();

            // Create the string to sign
            string stringToSign = $"{resourceUri}\n{expiration}";

            // Decode the base64 key
            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(sharedAccessKey);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Failed to decode shared access key. It must be base64 encoded.", nameof(sharedAccessKey), ex);
            }

            // Create the HMAC-SHA256 signature
            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
                string signature = Convert.ToBase64String(signatureBytes);

                // Create the SAS token
                // Note: Azure IoT SDK will handle URL encoding when needed for MQTT password
                string token = $"SharedAccessSignature sr={resourceUri}&sig={signature}&se={expiration}";
                return token;
            }
        }

        /// <summary>
        /// Generates a SAS token for Azure IoT Hub device
        /// </summary>
        /// <param name="hostName">IoT Hub hostname (e.g., "your-iothub.azure-devices.net")</param>
        /// <param name="deviceId">Device ID</param>
        /// <param name="sharedAccessKey">Shared access key (base64 encoded)</param>
        /// <param name="duration">Token validity duration (default: 1 hour)</param>
        /// <returns>SAS token string</returns>
        public static string GenerateDeviceSasToken(string hostName, string deviceId, string sharedAccessKey, TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromHours(1);
            string resourceUri = $"{hostName}/devices/{deviceId}";
            return GenerateSasToken(resourceUri, sharedAccessKey, duration.Value);
        }
    }
}

