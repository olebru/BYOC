// The phone page's scripts, kept out of phone.html so the Content Security Policy can forbid inline scripts.

try { document.documentElement.dataset.theme = localStorage.getItem('exuarch.theme') === 'light' ? 'light' : 'dark'; }
catch (e) { document.documentElement.dataset.theme = 'dark'; }

// Sends the address with the phone's own share sheet (to mail, a chat or another device), or copies it where
// there is none.
document.addEventListener('DOMContentLoaded', function () {
    const url = 'https://www.exuarch.com';
    const send = document.getElementById('send');
    const done = document.getElementById('done');
    function copied() { done.hidden = false; }
    send.addEventListener('click', function () {
        if (navigator.share) {
            navigator.share({ title: 'ExµArch', text: 'ExµArch, a computer architecture playground: open it on a computer.', url: url })
                .catch(function () { /* closed without sharing */ });
        } else if (navigator.clipboard) {
            navigator.clipboard.writeText(url).then(copied, function () { /* not allowed; the address is on the page */ });
        }
    });
});
