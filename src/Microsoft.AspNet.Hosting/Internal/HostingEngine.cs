// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting.Builder;
using Microsoft.AspNet.Hosting.Internal;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Hosting.Startup;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.AspNet.Hosting.Internal
{
    public class HostingEngine : IHostingEngine
    {
        private readonly IServiceCollection _applicationServiceCollection;
        private readonly IStartupLoader _startupLoader;
        private readonly ApplicationLifetime _applicationLifetime;
        private readonly IConfiguration _config;

        private IServiceProvider _applicationServices;

        // Only one of these should be set
        internal string StartupAssemblyName { get; set; }
        internal StartupMethods Startup { get; set; }
        internal Type StartupType { get; set; }

        // Only one of these should be set
        internal IServerFactory ServerFactory { get; set; }
        internal string ServerFactoryLocation { get; set; }
        private IServerInformation _serverInstance;

        public HostingEngine(IServiceCollection appServices, IStartupLoader startupLoader, IConfiguration config)
        {
            _config = config ?? new Configuration();
            _applicationServiceCollection = appServices;
            _startupLoader = startupLoader;
            _applicationLifetime = new ApplicationLifetime();
        }

        public virtual IDisposable Start()
        {
            EnsureApplicationServices();

            var application = BuildApplication();

            var _contextFactory = _applicationServices.GetRequiredService<IHttpContextFactory>();
            var _contextAccessor = _applicationServices.GetRequiredService<IHttpContextAccessor>();
            var server = ServerFactory.Start(_serverInstance,
                features =>
                {
                    var httpContext = _contextFactory.CreateHttpContext(features);
                    _contextAccessor.HttpContext = httpContext;
                    return application(httpContext);
                });

            return new Disposable(() =>
            {
                _applicationLifetime.NotifyStopping();
                server.Dispose();
                _applicationLifetime.NotifyStopped();
            });
        }

        private void EnsureApplicationServices()
        {
            EnsureStartup();

            _applicationServiceCollection.AddInstance<IApplicationLifetime>(_applicationLifetime);

            _applicationServices = Startup.ConfigureServicesDelegate(_applicationServiceCollection);
        }

        private void EnsureStartup()
        {
            if (Startup != null)
            {
                return;
            }

            if (StartupType == null)
            {
                var diagnosticTypeMessages = new List<string>();
                StartupType = _startupLoader.FindStartupType(StartupAssemblyName, diagnosticTypeMessages);
                if (StartupType == null)
                {
                    throw new ArgumentException(
                        diagnosticTypeMessages.Aggregate("Failed to find a startup type for the web application.", (a, b) => a + "\r\n" + b),
                        StartupAssemblyName);
                }
            }

            var diagnosticMessages = new List<string>();
            Startup = _startupLoader.LoadMethods(StartupType, diagnosticMessages);
            if (Startup == null)
            {
                throw new ArgumentException(
                    diagnosticMessages.Aggregate("Failed to find a startup entry point for the web application.", (a, b) => a + "\r\n" + b),
                    StartupAssemblyName);
            }
        }

        private RequestDelegate BuildApplication()
        {
            var builderFactory = _applicationServices.GetRequiredService<IApplicationBuilderFactory>();
            var builder = builderFactory.CreateBuilder();
            builder.ApplicationServices = _applicationServices;

            if (ServerFactory == null)
            {
                // Blow up if we don't have a server set at this point
                if (ServerFactoryLocation == null)
                {
                    throw new InvalidOperationException("IHostingBuilder.UseServer() is required for " + nameof(Start) + "()");
                }

                ServerFactory = _applicationServices.GetRequiredService<IServerLoader>().LoadServerFactory(ServerFactoryLocation);
            }

            _serverInstance = ServerFactory.Initialize(_config);
            builder.Server = _serverInstance;

            var startupFilters = _applicationServices.GetService<IEnumerable<IStartupFilter>>();
            var configure = Startup.ConfigureDelegate;
            foreach (var filter in startupFilters)
            {
                configure = filter.Configure(builder, configure);
            }

            configure(builder);

            return builder.Build();
        }

        public IServiceProvider ApplicationServices
        {
            get
            {
                EnsureApplicationServices();
                return _applicationServices;
            }
        }

        private class Disposable : IDisposable
        {
            private Action _dispose;

            public Disposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _dispose, () => { }).Invoke();
            }
        }
    }
}