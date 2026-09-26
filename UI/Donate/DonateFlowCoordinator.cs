using HMUI;
using Zenject;

namespace JDFixer.UI
{
    /// <summary>
    /// Hosts the donate screen as its own flow rather than a modal inside each gameplay tab.
    /// </summary>
    /// <remarks>
    /// The modal it replaces was duplicated verbatim across two markup files, needed a pair of
    /// <c>EmitEvent</c> calls to re-show itself, and had to be reopened after a late fetch in order to
    /// display its text. A flow gets a real back button from the game, so the dismiss path is the same
    /// one the player already uses everywhere else.
    /// </remarks>
    internal sealed class DonateFlowCoordinator : FlowCoordinator
    {
        /// <summary>
        /// The flow to return to on dismiss. Assigned by the caller immediately before presenting,
        /// because the tabs live inside whichever flow is currently showing the gameplay setup.
        /// </summary>
        internal FlowCoordinator _parentFlow;

        private DonateViewController _view;

        /// <remarks>
        /// Bound as a Unity component, so construction is an injected method rather than a constructor.
        /// </remarks>
        [Inject]
        private void Construct(DonateViewController view) => _view = view;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            showBackButton = true;
            SetTitle("Support JDFixer");

            ProvideInitialViewControllers(_view);
        }

        protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

        /// <summary>
        /// Hands control back to whoever presented this flow.
        /// </summary>
        /// <remarks>
        /// A flow coordinator cannot dismiss itself - <c>DismissFlowCoordinator</c> has to be called on
        /// the parent - so a missing parent is a programming error rather than a state to recover from.
        /// It is logged instead of thrown, because this runs from a button handler on the main thread.
        /// </remarks>
        private void Dismiss()
        {
            FlowCoordinator parent = _parentFlow;

            if (parent == null)
            {
                Plugin.Log.Error("Donate flow has no parent flow to dismiss back to");
                return;
            }

            parent.DismissFlowCoordinator(this);
        }
    }
}
