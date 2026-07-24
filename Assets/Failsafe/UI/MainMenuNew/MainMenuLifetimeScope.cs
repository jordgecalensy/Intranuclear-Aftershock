using VContainer;
using VContainer.Unity;

namespace Failsafe.UI.MainMenuNew
{
    public sealed class MainMenuLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<global::MainMenu>();
        }
    }
}
