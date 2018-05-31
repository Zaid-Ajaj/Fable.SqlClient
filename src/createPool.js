const mssql = require('mssql');

const createConnectionPool = function(config) {
    return new mssql.ConnectionPool(config);
}

module.exports = {
    createConnectionPool
}