window.infiniteScroll = {
    _observers: new Map(),

    observe: function (elementId, dotnetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // Disconnect any existing observer for this element
        this.unobserve(elementId);

        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting) {
                    dotnetRef.invokeMethodAsync('OnSentinelVisible');
                }
            },
            { rootMargin: '200px' } // trigger 200px before the sentinel is visible
        );

        observer.observe(el);
        this._observers.set(elementId, observer);
    },

    unobserve: function (elementId) {
        const observer = this._observers.get(elementId);
        if (observer) {
            observer.disconnect();
            this._observers.delete(elementId);
        }
    }
};
