window.notificationSurface = {
    observe: function (reference) {
        const observer = new ResizeObserver(entries => {
            const height = document.querySelector('.toast-stack')?.getBoundingClientRect().height || document.body.scrollHeight;
            reference.invokeMethodAsync('ContentResized', height);
        });
        observer.observe(document.body);
        window.addEventListener('message', event => {
            const frames = [...document.querySelectorAll('iframe[data-notification]')];
            const frame = frames.find(candidate => candidate.contentWindow === event.source);
            if (frame && event.data && typeof event.data.action === 'string')
                reference.invokeMethodAsync('TemplateAction', frame.dataset.notification, event.data.action, String(event.data.reply || ''));
        });
        return true;
    }
};
