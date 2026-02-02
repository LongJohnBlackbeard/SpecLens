using JdeClient.Core.Internal;
using NSubstitute;

namespace JdeClient.Core.UnitTests.JdeClientCore;

internal static class TestHelpers
{
    internal static void SetupExecuteAsync<T>(IJdeSession session)
    {
        session.ExecuteAsync(Arg.Any<Func<T>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Func<T>>();
                try
                {
                    return Task.FromResult(action());
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            });
    }

    internal static void SetupExecuteAsync(IJdeSession session)
    {
        session.ExecuteAsync(Arg.Any<Action>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Action>();
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            });
    }
}
