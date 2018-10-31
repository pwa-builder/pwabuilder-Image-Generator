/**
 * When an app window enters compact overlay mode it’ll be shown above other windows so it won’t get blocked. This allows users to continue to keep an eye on your app's content even when they are working with something else. The canonical example of an app taking advantage of this feature is a media player or a video chat app.
 * This snippet allows you to switch into compact overlay mode or return to the default mode.
 *
 * @alias Toggle Compact Overlay mode
 * @method toggleCompactOverlayMode
 * @param {boolean} [forceCompactOverlay=false] Force Compact Overlay mode.
 * @returns {Promise} Promise with new mode value (1=CompactOverlay | 0=Default).
 * @see https://docs.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.applicationviewmode
 */
function toggleCompactOverlayMode(forceCompactOverlay = false) {
    if (!window.Windows) return Promise.resolve("unsupported");

    var applicationView = Windows.UI.ViewManagement.ApplicationView;
    var currentMode = applicationView.getForCurrentView().viewMode;

    var newMode = (currentMode == Windows.UI.ViewManagement.ApplicationViewMode.default) || forceCompactOverlay
        ? Windows.UI.ViewManagement.ApplicationViewMode.compactOverlay
        : Windows.UI.ViewManagement.ApplicationViewMode.default;

    return applicationView.getForCurrentView()
        .tryEnterViewModeAsync(newMode)
        .then(() => newMode);
}