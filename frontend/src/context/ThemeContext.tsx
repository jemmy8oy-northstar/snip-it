import React, { useEffect, useState } from 'react';
import { ThemeContext, type Theme } from './theme-context';

/**
 * snip-it wears the design system's `studio` theme (claude-code-bot#53:
 * "Happy to green light studio").
 *
 * Two attributes now, not one. `data-theme` names the THEME and does not change
 * here; `data-mode` is the light/dark axis. That split is the design system's,
 * and it is what lets the theme be swapped without touching this file — putting
 * 'light'/'dark' straight into `data-theme` conflated "which brand" with "which
 * mode" and made them impossible to vary independently.
 *
 * NOTE: studio is DARK-FIRST upstream — the bare `[data-theme='studio']`
 * selector IS the dark theme and `[data-mode='light']` is the opt-out. This app
 * still DEFAULTS to light, deliberately: changing snip-it's default is a product
 * decision rather than a token swap, so it is raised on the adoption PR instead
 * of being smuggled into it.
 */
const THEME_NAME = 'studio';

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [theme, setTheme] = useState<Theme>(() => {
        const saved = localStorage.getItem('theme');
        return (saved as Theme) || 'light';
    });

    useEffect(() => {
        document.documentElement.setAttribute('data-theme', THEME_NAME);
        document.documentElement.setAttribute('data-mode', theme);
        localStorage.setItem('theme', theme);
    }, [theme]);

    const toggleTheme = () => {
        setTheme(prev => (prev === 'light' ? 'dark' : 'light'));
    };

    return (
        <ThemeContext.Provider value={{ theme, toggleTheme }}>
            {children}
        </ThemeContext.Provider>
    );
};
