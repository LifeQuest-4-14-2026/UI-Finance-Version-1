using Microsoft.EntityFrameworkCore;
using ProductMasterPlanV1.Core.Contract;
using ProductMasterPlanV1.Core.Engine;
using ProductMasterPlanV1.Infrastructure;
using ProductMasterPlanV1.Infrastructure.Data;
using ProductMasterPlanV1.Infrastructure.Services;
using System.Windows;

namespace ProductMasterPlanV1.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            IV1ApplicationService service = CreateV1ApplicationService();

            var window = new MainWindow(service);
            MainWindow = window;
            window.Show();
        }

        private static IV1ApplicationService CreateV1ApplicationService()
        {
            var options = new DbContextOptionsBuilder<V1DbContext>()
                .UseSqlite("Data Source=productmasterplanv1.db")
                .Options;

            var dbContext = new V1DbContext(options);

            // Quick first-run fix:
            dbContext.Database.EnsureCreated();

            IV1Engine engine = new V1Engine();

            return new V1ApplicationService(dbContext, engine);
        }
    }
}