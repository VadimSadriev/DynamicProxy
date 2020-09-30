using System;
using System.Linq;
using Castle.DynamicProxy;
using DynamicProxy.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using IInvocation = Castle.DynamicProxy.IInvocation;

namespace DynamicProxy.Tests
{
    /// <summary>
    /// Юнит тесты связанные с Castle.DynamicProxy
    /// <remarks>
    /// http://www.castleproject.org/projects/dynamicproxy/
    /// </remarks>
    /// </summary>
    public class CastleDynamicProxyTests
    {
        private readonly ProxyGenerator _proxyGenerator;
        private readonly FooService _sut;
        private readonly Mock<ILogger<FooService>> _loggerMock = new Mock<ILogger<FooService>>();
        private readonly Mock<ILogger<LoggerInterceptor>> _loggerInterceptorMock = new Mock<ILogger<LoggerInterceptor>>();

        public CastleDynamicProxyTests()
        {
            _proxyGenerator = new ProxyGenerator();

            _sut = new FooService(_loggerMock.Object);
        }

        [Fact(Skip = "Require parametrless constructor")]
        public void ShouldCreateProxy()
        {
            // arrange
            // act
            var proxy = _proxyGenerator.CreateClassProxy<FooService>();

            // assert
            proxy.Should().BeAssignableTo<FooService>();
            proxy.Should().BeAssignableTo<IProxyService>();
        }

        [Fact]
        public void ShouldCreateProxyWithInterface()
        {
            // arrange
            // act
            var proxy = (IFooService)_proxyGenerator.CreateInterfaceProxyWithTarget(typeof(IFooService), _sut);

            // assert
            proxy.Should().BeAssignableTo<IProxyService>();
            proxy.Should().BeAssignableTo<IFooService>();
        }

        [Fact]
        public void ShouldExecuteAsNormal()
        {
            // arrange
            var proxy = (IFooService)_proxyGenerator.CreateInterfaceProxyWithTarget(typeof(IFooService), _sut);
            _loggerMock.MockLog(LogLevel.Information);

            // act
            proxy.DoServiceWork();

            // assert
            _loggerMock.VerifyLogger(LogLevel.Information, "Doing service work", Times.Once());
        }

        [Fact]
        public void ShouldExecuteWithInterceptor()
        {
            // arrange

            var interceptors = new IInterceptor[]
            {
                new LoggerInterceptor(_loggerInterceptorMock.Object),
            };

            var proxy = (IFooService)_proxyGenerator.CreateInterfaceProxyWithTarget(typeof(IFooService), _sut, interceptors);

            _loggerInterceptorMock.MockLog(LogLevel.Information);

            // act
            proxy.DoServiceWork();

            // assert
            _loggerInterceptorMock.VerifyLogger(LogLevel.Information, $"Calling method: {nameof(IFooService.DoServiceWork)}", Times.Once());
        }

        [Fact]
        public void ShouldFillServiceProviderWithProxies()
        {
            // arrange
            var services = new ServiceCollection();
            services.AddLogging(x => x.AddConsole());
            services.AddTransient<IFooService, FooService>();
            services.AddTransient<IInterceptor, LoggerInterceptor>();

            services.ReplaceWithProxy();
            
            var serviceProvider = services.BuildServiceProvider();

            // act
            var proxyFooService = serviceProvider.GetRequiredService<IFooService>();
            proxyFooService.DoServiceWork();

            // assert
            proxyFooService.Should().BeAssignableTo<IProxyService>();
        }
    }

    public interface IFooService : IProxyService
    {
        void DoServiceWork();
    }

    public class FooService : IFooService
    {
        private readonly ILogger<FooService> _logger;

        public FooService(ILogger<FooService> logger)
        {
            _logger = logger;
        }

        public void DoServiceWork()
        {
            _logger.LogInformation("Doing service work");
        }
    }

    public class LoggerInterceptor : IInterceptor
    {
        private readonly ILogger<LoggerInterceptor> _logger;

        public LoggerInterceptor(ILogger<LoggerInterceptor> logger)
        {
            _logger = logger;
        }

        public void Intercept(IInvocation invocation)
        {
            _logger.LogInformation("Calling method: {name}", invocation.Method.Name);
            invocation.Proceed();
        }
    }
}