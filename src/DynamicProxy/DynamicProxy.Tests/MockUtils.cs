using System;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;

namespace DynamicProxy.Tests
{
    public static class MockUtils
    {
        public static ISetup<ILogger<T>> MockLog<T>(this Mock<ILogger<T>> logger, LogLevel level)
        {
            return logger.Setup(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<object>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<object, Exception, string>>()));
        }

        public static void VerifyLogger<T>(this Mock<ILogger<T>> mock, LogLevel level, string text, Times times)
        {
            mock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => string.Equals(text, o.ToString(), StringComparison.InvariantCultureIgnoreCase)),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), times);
        }
    }
}