import { createContext, useContext } from 'react';

export type Theme = 'light' | 'dark';

export interface ThemeContextType {
    theme: Theme;
    toggleTheme: () => void;
}

/** Kept out of ThemeContext.tsx so that file only exports components
 *  (react-refresh/only-export-components). Mirrors web-template#81. */
export const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

export const useTheme = () => {
    const context = useContext(ThemeContext);
    if (context === undefined) {
        throw new Error('useTheme must be used within a ThemeProvider');
    }
    return context;
};
