const colors = require('tailwindcss/colors')

module.exports = {
    content: [
        './wwwroot/index.html',
        './**/*.razor'
    ],
    darkMode: 'class',
    theme: {
        extend: {
            colors: {
                cyan: colors.cyan
            }
        },
    },
    plugins: []
}