export const UI_BASE_WIDTH = 1280;
export const UI_BASE_HEIGHT = 720;

const minimumScale = 0.01;

function viewportSize() {
  return {
    width: Math.max(window.innerWidth, 1),
    height: Math.max(window.innerHeight, 1)
  };
}

function updateViewportScale() {
  const { width, height } = viewportSize();
  const scale = Math.max(
    minimumScale,
    Math.min(width / UI_BASE_WIDTH, height / UI_BASE_HEIGHT)
  );

  document.documentElement.style.setProperty('--ui-scale', scale.toFixed(6));
}

export function initializeViewportScale() {
  updateViewportScale();
  window.addEventListener('resize', updateViewportScale, { passive: true });
  window.visualViewport?.addEventListener('resize', updateViewportScale, { passive: true });
}
