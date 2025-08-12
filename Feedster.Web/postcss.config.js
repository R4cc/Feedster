module.exports = ({ env }) => ({
    plugins: {
        "@tailwindcss/postcss": {},
        autoprefixer: {},
        cssnano: env === "production" ? { preset: "default" } : false
    }
});