const editableInputTypes = new Set([
  'email',
  'number',
  'password',
  'search',
  'tel',
  'text',
  'url',
]);

export const isEditableShortcutTarget = (target) => {
  if (!target) return false;
  const tagName = target.tagName?.toLowerCase();

  if (
    target.isContentEditable ||
    target.getAttribute?.('contenteditable') === 'true'
  ) {
    return true;
  }
  if (tagName === 'textarea' || tagName === 'select') return true;
  if (tagName !== 'input') return false;

  const type = target.getAttribute('type') || 'text';
  return editableInputTypes.has(type.toLowerCase());
};

export const getPlayerShortcutAction = (event = {}) => {
  if (
    event.defaultPrevented ||
    event.altKey ||
    event.ctrlKey ||
    event.metaKey ||
    isEditableShortcutTarget(event.target)
  ) {
    return null;
  }

  switch (event.key) {
    case ' ':
    case 'Spacebar':
    case 'k':
    case 'K':
      return 'togglePlayback';
    case 'ArrowLeft':
      return event.shiftKey ? 'previous' : 'seekBackward';
    case 'ArrowRight':
      return event.shiftKey ? 'next' : 'seekForward';
    case 'm':
    case 'M':
      return 'toggleMute';
    case 'e':
    case 'E':
      return 'toggleEqualizer';
    case 'l':
    case 'L':
      return 'toggleLyrics';
    case 'v':
    case 'V':
      return 'toggleVisualizer';
    default:
      return null;
  }
};
