using System;
using System.Linq;
using Castle.DynamicProxy;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Internals;
using Scrutor;
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

            ReplaceWithProxy(services);
            var serviceProvider = services.BuildServiceProvider();

            // act
            var proxyFooService = serviceProvider.GetRequiredService<IFooService>();
            proxyFooService.DoServiceWork();

            // assert
            proxyFooService.Should().BeAssignableTo<IProxyService>();
        }

        private void ReplaceWithProxy(IServiceCollection services)
        {
            // Берём из контейнера все сервисы которые должны перейти в прокси
            var proxyServiceDescriptors = services
                .Where(x => x.ServiceType.GetInterfaces().Contains(typeof(IProxyService)))
                .ToList();

            foreach (var serviceDescriptor in proxyServiceDescriptors)
            {
                // удаляем текующую реализацию в контейнере
                services.Remove(serviceDescriptor);

                // берём первый конструктор (не учитываем кейс с мнонежством конструкторов. нужно заморачиваться)
                var constructorInfo = serviceDescriptor.ImplementationType.GetConstructors()
                    .ToList()
                    .FirstOrDefault();

                var constructorParameters = constructorInfo.GetParameters().ToList();

                // типы зависимостей конструктора для последующего вытаскивания из контейнера
                var dependencyTypes = constructorParameters.Select(x => x.ParameterType).ToList();

                object ImplFactory(IServiceProvider provider)
                {
                    // список всех зависимостей текущего типа в контейнере
                    // var serviceDependencies = new List<object>();
                    var serviceDependencies = new object[dependencyTypes.Count];

                    // достаём все зависимости конструктора
                    for (var i = 0; i < dependencyTypes.Count; i++)
                    {
                        var service = provider.GetRequiredService(dependencyTypes[i]);
                        serviceDependencies[i] = service;
                    }

                    var interceptors = provider.GetServices<IInterceptor>().ToArray();

                    // Создаём прокси прокидывая зависимости в конструктор
                    // порядок аргументов должен быть соответствующим сервисам забраным из контейнера
                    var serviceToProxy = Activator.CreateInstance(serviceDescriptor.ImplementationType, serviceDependencies);

                    var proxy = _proxyGenerator.CreateInterfaceProxyWithTarget(serviceDescriptor.ServiceType, serviceToProxy, interceptors);

                    return proxy;
                }

                var proxyServiceDescriptor = new ServiceDescriptor(serviceDescriptor.ServiceType, ImplFactory, serviceDescriptor.Lifetime);
                // добавляем реализацию с прокси
                services.Add(proxyServiceDescriptor);
            }
        }
    }


    public interface IProxyService
    {
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