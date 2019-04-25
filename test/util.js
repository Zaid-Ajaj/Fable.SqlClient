module.exports = {
    envVars: () => {
        var keys = Object.keys(process.env);
        return keys.map(key => [key, process.env[key]])
    }
}