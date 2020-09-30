using System;
using System.Linq;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicProxy.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection ReplaceWithProxy(this IServiceCollection services)
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

                    var proxyGenerator = new ProxyGenerator();

                    var proxy = proxyGenerator.CreateInterfaceProxyWithTarget(serviceDescriptor.ServiceType, serviceToProxy, interceptors);

                    return proxy;
                }

                var proxyServiceDescriptor = new ServiceDescriptor(serviceDescriptor.ServiceType, ImplFactory, serviceDescriptor.Lifetime);
                // добавляем реализацию с прокси
                services.Add(proxyServiceDescriptor);
            }

            return services;
        }
    }
}