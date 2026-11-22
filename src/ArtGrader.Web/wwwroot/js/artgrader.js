window.artGrader = {
    size: element => {
        const rect = element.getBoundingClientRect();
        return [rect.width, rect.height];
    },
};
