### Why the service runs as SYSTEM

The default installation configures `IoTPowerShellAgent` to run as `NT AUTHORITY\SYSTEM`.

This is an intentional design decision for an endpoint-management and automation agent rather than a requirement of Azure IoT Hub or PowerShell itself.

The agent is intended to perform administrative operations on the local Windows host, including operations that may require privileges unavailable to a standard user context. Running the service under a dedicated low-privilege account would prevent certain classes of system administration tasks from functioning correctly and could introduce inconsistent privilege behaviour between interactive and remotely executed automation.

This is consistent with the execution model used by established endpoint-management agents. For example, the Datto RMM Agent runs as `NT AUTHORITY\SYSTEM` on Windows so that components and policies can perform system-level operations without depending on an interactive user's UAC context.

The important security boundary is therefore not the Windows service account alone, but **who is authorized to submit execution requests to the agent**.

```text
                    Trust Boundary
                         │
                         ▼
              ┌─────────────────────┐
              │      Azure IoT Hub   │
              │                     │
              │ Authentication      │
              │ Authorization       │
              └──────────┬──────────┘
                         │
                  ExecuteScript
                         │
                         ▼
              ┌─────────────────────┐
              │ IoTPowerShellAgent  │
              │                     │
              │ SYSTEM              │
              │       │             │
              │       ▼             │
              │ PowerShell Runtime  │
              └─────────────────────┘
                         │
                         ▼
                  Windows Host
```

Because the agent provides privileged remote execution, compromise of the IoT Hub authorization boundary or the agent itself could result in SYSTEM-level code execution on the endpoint.

Accordingly, deployments should treat the agent as a privileged management component and apply appropriate controls around:

- IoT Hub authentication and authorization
- device and module identity
- who or what can invoke direct methods
- credential and secret storage
- network exposure
- execution auditing
- telemetry and monitoring
- service installation and configuration

The use of SYSTEM should therefore be understood as a **privilege requirement of the endpoint-management workload**, not as a security boundary.

Where a deployment does not require system-level operations, the service should be configured to run under a more restricted identity.
