import {
  getPlayerShortcutAction,
  isEditableShortcutTarget,
} from './playerShortcuts';

describe('playerShortcuts', () => {
  it('maps transport and panel keys to player actions', () => {
    expect(getPlayerShortcutAction({ key: ' ' })).toBe('togglePlayback');
    expect(getPlayerShortcutAction({ key: 'K' })).toBe('togglePlayback');
    expect(getPlayerShortcutAction({ key: 'ArrowLeft' })).toBe('seekBackward');
    expect(getPlayerShortcutAction({ key: 'ArrowRight' })).toBe('seekForward');
    expect(getPlayerShortcutAction({ key: 'ArrowLeft', shiftKey: true })).toBe(
      'previous',
    );
    expect(getPlayerShortcutAction({ key: 'ArrowRight', shiftKey: true })).toBe(
      'next',
    );
    expect(getPlayerShortcutAction({ key: 'm' })).toBe('toggleMute');
    expect(getPlayerShortcutAction({ key: 'e' })).toBe('toggleEqualizer');
    expect(getPlayerShortcutAction({ key: 'l' })).toBe('toggleLyrics');
    expect(getPlayerShortcutAction({ key: 'v' })).toBe('toggleVisualizer');
  });

  it('ignores modified or already-handled key events', () => {
    expect(getPlayerShortcutAction({ ctrlKey: true, key: 'k' })).toBeNull();
    expect(getPlayerShortcutAction({ defaultPrevented: true, key: 'k' })).toBeNull();
    expect(getPlayerShortcutAction({ altKey: true, key: 'ArrowRight' })).toBeNull();
    expect(getPlayerShortcutAction({ key: 'x', shiftKey: true })).toBeNull();
  });

  it('ignores editable targets', () => {
    const input = document.createElement('input');
    const button = document.createElement('button');
    const richText = document.createElement('div');
    richText.setAttribute('contenteditable', 'true');

    expect(isEditableShortcutTarget(input)).toBe(true);
    expect(isEditableShortcutTarget(richText)).toBe(true);
    expect(isEditableShortcutTarget(button)).toBe(false);
    expect(getPlayerShortcutAction({ key: 'k', target: input })).toBeNull();
  });
});
