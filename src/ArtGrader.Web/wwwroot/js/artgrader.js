window.artGrader = {
    size: element => {
        const rect = element.getBoundingClientRect();
        return [rect.width, rect.height];
    },
    selection: element => [element.selectionStart ?? 0, element.selectionEnd ?? 0],
    value: element => element.value,
    quit: () => {
        const form = document.createElement('form');
        form.method = 'post';
        form.action = '/beenden';
        document.body.appendChild(form);
        form.submit();
    },
};
