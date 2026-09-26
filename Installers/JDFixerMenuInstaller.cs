using JDFixer.Managers;
using JDFixer.UI;
using Zenject;

namespace JDFixer.Installers
{
    internal sealed class JDFixerMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            // BindInterfacesTo binds each implementation to every interface it exposes, which is how
            // the manager receives List<IBeatmapInfoUpdater> and List<IRefreshable> without naming the
            // concrete UI types.
            Container.BindInterfacesTo<JDFixerUIManager>().AsSingle();
            Container.BindInterfacesTo<MainMenuUI>().AsSingle();
            Container.BindInterfacesTo<CustomOnlineUI>().AsSingle();
            Container.BindInterfacesTo<ModifierUI>().AsSingle();

            // Flow Coordinators need to binded like this, as a component since it is a Unity Component
            Container.Bind<PreferencesFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            Container.Bind<DonateFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();

            // Even though ViewControllers are also Unity Components, we bind them with this helper method provided by SiraUtil (FromNewComponentAsViewController)
            Container.Bind<JDPreferencesListViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<RTPreferencesListViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<DonateViewController>().FromNewComponentAsViewController().AsSingle();
        }
    }
}