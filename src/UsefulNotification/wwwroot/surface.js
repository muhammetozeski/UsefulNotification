window.notificationSurface = {
    reference: null,
    pending: false,
    dialog: null,
    interaction: null,
    focusDialog: function () {
        const dialog = document.querySelector('[role="alertdialog"]');
        if (dialog && this.dialog !== dialog) dialog.querySelector('button')?.focus();
        this.dialog = dialog;
    },
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
        addEventListener('pointerdown', event => {
            const area = event.target.closest('[data-window-hit]');
            if (!area || event.button !== 0) return;
            event.preventDefault();
            const interaction = { area, pointerId: event.pointerId, pending: false };
            this.interaction = interaction;
            area.setPointerCapture(event.pointerId);
            interaction.ready = reference.invokeMethodAsync('BeginWindowInteraction', Number(area.dataset.windowHit));
        });
        const finishInteraction = () => {
            const interaction = this.interaction;
            if (!interaction) return;
            this.interaction = null;
            if (interaction.area.hasPointerCapture(interaction.pointerId)) interaction.area.releasePointerCapture(interaction.pointerId);
            reference.invokeMethodAsync('EndWindowInteraction');
        };
        addEventListener('pointermove', event => {
            const interaction = this.interaction;
            if (!interaction || event.pointerId !== interaction.pointerId) return;
            if ((event.buttons & 1) === 0) { finishInteraction(); return; }
            if (interaction.pending) return;
            interaction.pending = true;
            requestAnimationFrame(async () => {
                try { await interaction.ready; if (this.interaction === interaction) await reference.invokeMethodAsync('UpdateWindowInteraction'); }
                finally { interaction.pending = false; }
            });
        });
        addEventListener('pointerup', finishInteraction);
        addEventListener('pointercancel', finishInteraction);
        addEventListener('lostpointercapture', finishInteraction);
        addEventListener('blur', finishInteraction);
        addEventListener('keydown', event => {
            const dialog = document.querySelector('[role="alertdialog"]');
            if (event.key !== 'Tab' || !dialog) return;
            const buttons = [...dialog.querySelectorAll('button:not([disabled])')];
            const index = buttons.indexOf(document.activeElement);
            if (event.shiftKey && index <= 0) { event.preventDefault(); buttons.at(-1)?.focus(); }
            else if (!event.shiftKey && index === buttons.length - 1) { event.preventDefault(); buttons[0]?.focus(); }
        });
        addEventListener('message', event => {
            const frame = [...document.querySelectorAll('iframe[data-notification]')].find(item => item.contentWindow === event.source);
            if (!frame) return;
            if (Number.isFinite(event.data?.height) && event.data.height > 0 && frame.style.height !== event.data.height + 'px') frame.style.height = event.data.height + 'px';
            if (typeof event.data?.action === 'string') reference.invokeMethodAsync('TemplateAction', frame.dataset.notification, frame.dataset.occurrence, Number(frame.dataset.revision), event.data.action, String(event.data.reply || ''));
        });
        this.report();
    }
};
