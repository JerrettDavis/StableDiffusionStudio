window.clipboardHelper = {
    copy: function (text) {
        navigator.clipboard.writeText(text);
    }
};

window.imageDropZone = {
    _listeners: new Map(),

    init: function (element, dotNetRef) {
        if (!element) return;

        const handler = async (e) => {
            const items = e.clipboardData?.items;
            if (!items) return;
            for (const item of items) {
                if (item.type.startsWith('image/')) {
                    e.preventDefault();
                    const file = item.getAsFile();
                    if (file) {
                        const bytes = await file.arrayBuffer();
                        const base64 = btoa(String.fromCharCode(...new Uint8Array(bytes)));
                        dotNetRef.invokeMethodAsync('OnImagePasted', base64, file.name || 'pasted-image.png');
                    }
                    return;
                }
            }
        };

        document.addEventListener('paste', handler);
        this._listeners.set(element, handler);
    },

    dispose: function (element) {
        const handler = this._listeners.get(element);
        if (handler) {
            document.removeEventListener('paste', handler);
            this._listeners.delete(element);
        }
    }
};
