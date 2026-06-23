import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * Custom hook to manage focus on route changes for accessibility.
 * Automatically moves focus to the main content area when navigating between pages.
 */
export function useFocusOnNavigate() {
  const location = useLocation();
  const previousLocation = useRef(location);

  useEffect(() => {
    // Only move focus if the pathname actually changed
    if (previousLocation.current.pathname !== location.pathname) {
      // Find the main content element
      const mainContent = document.getElementById('main-content');
      if (mainContent) {
        // Make it focusable temporarily if it doesn't have tabindex
        const hadTabIndex = mainContent.hasAttribute('tabindex');
        if (!hadTabIndex) {
          mainContent.setAttribute('tabindex', '-1');
        }

        // Move focus
        mainContent.focus();

        // Remove tabindex if we added it (to prevent polluting tab order)
        if (!hadTabIndex) {
          mainContent.addEventListener('blur', () => {
            mainContent.removeAttribute('tabindex');
          }, { once: true });
        }
      }

      previousLocation.current = location;
    }
  }, [location]);
}
