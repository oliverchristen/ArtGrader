window.artGrader = {
    size: element => {
        const rect = element.getBoundingClientRect();
        return [rect.width, rect.height];
    },
    selection: element => [element.selectionStart ?? 0, element.selectionEnd ?? 0],
    value: element => element.value,
};
