// Single-key shortcuts for the reply composer (a approve, e edit, r reject), as in a mail client.
// Keys typed into a field, or with a modifier, are left alone.
let target = null;

function onKey(event) {
  if (!target || event.ctrlKey || event.metaKey || event.altKey || event.repeat) return;
  const el = event.target;
  if (el && (el.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName))) return;
  const key = event.key.toLowerCase();
  if (key === 'a' || key === 'e' || key === 'r') {
    event.preventDefault();
    target.invokeMethodAsync('OnShortcutAsync', key);
  }
}

export function listen(dotnet) {
  target = dotnet;
  document.addEventListener('keydown', onKey);
}

export function stop() {
  document.removeEventListener('keydown', onKey);
  target = null;
}
