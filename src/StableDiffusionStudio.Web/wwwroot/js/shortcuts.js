window.shortcutHelper = {
    registerGenerateShortcut: function (dotnetRef) {
        var handler = function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('OnGenerateShortcut');
            }
        };
        document.addEventListener('keydown', handler);
        return handler;
    },
    unregisterGenerateShortcut: function (handler) {
        if (handler) {
            document.removeEventListener('keydown', handler);
        }
    }
};
