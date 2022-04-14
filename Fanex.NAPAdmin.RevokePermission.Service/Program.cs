namespace Fanex.NAPAdmin.RevokePermission.Service
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using Data;
    using Data.OldConnectionStringProvider;
    using Fanex.Logging.Sentry.RavenClient;
    using Logging;
    using SharpRaven;
    using Topshelf;

    internal static class Program
    {
        private static void Main(string[] args)
        {
            ConfigureFanexData();
            ConfigureFanexLogging();
            ConfigureService();
        }

        private static void ConfigureFanexData()
        {
            try
            {
                DbSettingProviderManager
                    .StartNewSession()
                    .Use(new OldConnectionStringProvider())
                    .UseDefaultDbSettingProvider(
                        resourcePath: "App_Data/",
                        ignoreRedundantParameters: false,
                        enableWatching: true)
                    .Run();
            }
            catch (Exception exception)
            {
                LogService.Error("Revoke User Permission Service: Error when Fanex.Data start new session", exception);
            }
        }

        private static void ConfigureFanexLogging()
        {
            LogManager
                .SetDefaultLogCategory(ConfigurationManager.AppSettings["SiteCategory"])
                .Use(new List<ILogEngine>
                {
                    new SentryLogging(new SentryEngineOptions
                    {
                        Dsn = new Dsn(ConfigurationManager.AppSettings["SentryToken"])
                    })
                });
        }

        private static void ConfigureService()
        {
            HostFactory.Run(configure =>
            {
                configure.Service<UserPermission>(service =>
                {
                    service.ConstructUsing(s => new UserPermission());
                    service.WhenStarted(s => s.Revoke());
                    service.WhenStopped(s => s.OnStop());
                });

                configure.SetServiceName("Fanex.NAP.Admin.RevokePermission.Service");
                configure.SetDisplayName("Fanex.NAP.Admin.RevokePermission.Service");
                configure.SetDescription("Revoke user permission daily");

                configure.RunAsLocalSystem();
                configure.EnableShutdown();
            });
        }
    }
}