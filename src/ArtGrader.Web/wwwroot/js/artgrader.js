window.artGrader = {
    size: element => {
        const rect = element.getBoundingClientRect();
        return [rect.width, rect.height];
    },
    quit: () => {
        const form = document.createElement('form');
        form.method = 'post';
        form.action = '/beenden';
        document.body.appendChild(form);
        form.submit();
    },
};
