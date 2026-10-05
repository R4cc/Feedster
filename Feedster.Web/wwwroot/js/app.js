(() => {
    const openers = new WeakMap();
    function updateScrollLock() {
        document.body.classList.toggle('dialog-open', Boolean(document.querySelector('dialog[open]')));
    }
    function openDialog(dialog) {
        if (!dialog || dialog.open) return;
        openers.set(dialog, document.activeElement);
        dialog.addEventListener('close', () => {
            updateScrollLock();
            const opener = openers.get(dialog);
            if (opener?.isConnected) opener.focus();
        }, { once: true });
        dialog.showModal();
        updateScrollLock();
    }
    function closeDialog(dialog) {
        if (dialog?.open) dialog.close();
        updateScrollLock();
    }
    window.feedster = {
        closeDialog,
        openConfirmation(dialog, reference) {
            dialog.addEventListener('cancel', event => {
                event.preventDefault();
                reference.invokeMethodAsync('Cancel');
            }, { once: true });
            openDialog(dialog);
        }
    };
    window.toggleSlideover = () => {
        const dialog = document.getElementById('slideover');
        if (dialog?.open) closeDialog(dialog); else openDialog(dialog);
    };
    window.closeSlideover = () => closeDialog(document.getElementById('slideover'));
    window.OnScrollEvent = () => {
        window.scrollTo({ top: 0, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
        document.querySelector('h1')?.focus({ preventScroll: true });
    };
    new MutationObserver(updateScrollLock).observe(document.body, { childList: true, subtree: true });
})();
