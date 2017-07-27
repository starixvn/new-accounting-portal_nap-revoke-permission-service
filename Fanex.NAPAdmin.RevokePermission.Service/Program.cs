namespace Fanex.NAPAdmin.RevokePermission.Service
{
    using System;
    using Data;
    using Data.OldConnectionStringProvider;
    using Logging;
    using Topshelf;

    internal static class Program
    {
        private static void Main(string[] args)
        {
            ConfigureFanexData();
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

                configure.SetServiceName("Fanex.NAPAdmin.RevokePermission.Service");
                configure.SetDisplayName("Fanex.NAPAdmin.RevokePermission.Service");
                configure.SetDescription("Revoke user permission daily");

                configure.RunAsLocalSystem();
                configure.EnableShutdown();
            });
        }
    }
}