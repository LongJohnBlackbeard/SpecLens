# Logging Guide

Spec Lens supports two separate log streams:
- App log: UI, workflow, and application-level events.
- JDE client log: debug output from the JdeClient.Core library.

Both are off by default and can be enabled independently.

## Where logs are written
By default, logs are written under LocalAppData:

```
%LOCALAPPDATA%\SpecLens\Logs\App\spec-lens-YYYYMMDD.log
%LOCALAPPDATA%\SpecLens\Logs\JdeClient\jde-client-YYYYMMDD.log
```

You can change either path in Settings.

## Enable logging in the app
1. Open Settings.
2. Under Application:
   - Enable app logging.
   - (Optional) change the log file path.
   - Enable JDE client logging if you need low-level client debug details.
   - (Optional) change the client log path.
3. Reproduce the issue.
4. Collect the log files for the day the issue occurred.

## Enable logging when hosting JdeClient.Core
If you are using JdeClient.Core outside the Avalonia app, enable debug logging via options:

```csharp
var options = new JdeClientOptions
{
    EnableDebug = true,
    EnableSpecDebug = true,
    EnableQueryDebug = true,
    LogSink = message => /* write to your logger or file */
};

using var client = new JdeClient(options);
```

If you set a custom `LogSink`, make sure it redacts any sensitive data before writing logs.

## What is logged (and what is not)
App log:
- Connection and workflow events
- Errors and exceptions
- High-level query activity (table/view names, counts)

JDE client log:
- Debug output from native calls and query mechanics
- Data source resolution and request flow

Sensitive data:
- The app log redacts query filters and row data structures.
- The JDE client log is sanitized by the app before it is written.

Always review logs before sharing and remove anything sensitive.

## Sharing logs
When reporting an issue, include:
- App log for the date/time of the issue
- JDE client log if client debug logging was enabled
- A short description of the steps you took
