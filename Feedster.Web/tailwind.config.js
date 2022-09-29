const colors = require('tailwindcss/colors')

module.exports = {
    mode: 'jit',
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
    variants: {
        extend: { display: ['dark'], },
    },
    plugins: [
        require('tailwindcss-textshadow')
    ]
}