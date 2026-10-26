window.notificationSurface = {
    reference: null,
    pending: false,
    report: function () {
        if (this.pending) return;
        this.pending = true;
        requestAnimationFrame(async () => {
            this.pending = false;
            const stack = document.querySelector('.toast-stack');
            if (!stack) return;
            await this.reference.invokeMethodAsync('ContentResized', stack.getBoundingClientRect().height);
            const receipts = [...stack.querySelectorAll('[data-notification]')].map(card => {
                const box = card.getBoundingClientRect();
                return {
                    notificationId: card.dataset.notification,
                    occurrenceId: card.dataset.occurrence,
                    contentRevision: Number(card.dataset.revision),
                    visible: box.width > 0 && box.height > 0 && box.bottom > 0 && box.top < innerHeight && box.right > 0 && box.left < innerWidth
                };
            });
            this.reference.invokeMethodAsync('CardsRendered', receipts);
        });
    },
    observe: function (reference) {
        this.reference = reference;
        new ResizeObserver(() => this.report()).observe(document.body);
        new MutationObserver(() => this.report()).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-revision', 'data-occurrence', 'style'] });
        addEventListener('scroll', () => this.report(), true);
        addEventListener('resize', () => this.report());
        addEventListener('message', event => {
            const frame = [...document.querySelectorAll('iframe[data-notification]')].find(item => item.contentWindow === event.source);
            if (!frame) return;
            if (Number.isFinite(event.data?.height) && event.data.height > 0 && frame.style.height !== event.data.height + 'px') frame.style.height = event.data.height + 'px';
            if (typeof event.data?.action === 'string') reference.invokeMethodAsync('TemplateAction', frame.dataset.notification, frame.dataset.occurrence, Number(frame.dataset.revision), event.data.action, String(event.data.reply || ''));
        });
        this.report();
    }
};
