// adapted from https://stackoverflow.com/a/49041392
function table_sorting() {
    // gets the value of the specified child (by idx) of the given table row (tr)
    const getCellValue = (tr, idx) => tr.children[idx].innerText || tr.children[idx].textContent;

    // compares two table cells 
    // idx - index of cell
    // asc - boolean, true for ascending sort, false for descending
    const comparer = (idx, asc) => (a, b) => ((v1, v2) => {
        const splitRe = /\d+|\D+/g;
        // split into digit and non-digit parts
        const parts1 = v1.match(splitRe);
        const parts2 = v2.match(splitRe);

        if (parts1 && parts2) {
            // compare parts until unequal parts are found or there are no more parts
            for (let i = 0; i < Math.min(parts1.length, parts2.length); i++) {
                // compare the two parts either as numbers or strings
                let cmp = parts1[i] !== '' && parts2[2] !== '' &&
                !isNaN(parts1[i]) && !isNaN(parts2[i]) ? parts1[i] - parts2[i]
                    : parts1[i].toString().localeCompare(parts2[i]);
                // if the parts are not equal return the compare value
                if (cmp !== 0) {
                    return cmp;
                }
            }
        }
        // either equal or longer value has higher value
        return parts2.length - parts1.length;
    })(getCellValue(asc ? a : b, idx), getCellValue(asc ? b : a, idx));

    // do the work...
    document.querySelectorAll('th').forEach(th => th.addEventListener('click', (() => {
        const table = th.closest('table');
        const tbody = table.querySelector('tbody');
        Array.from(tbody.querySelectorAll('tr'))
            .sort(comparer(Array.from(th.parentNode.children).indexOf(th), this.asc = !this.asc))
            .forEach(tr => tbody.appendChild(tr) );
    })));
}