"use strict";

module.exports = {
  columnDefinitions: result => {
    var keys = Object.keys(result.recordset.columns);
    return keys.map(key => {
      return [key, result.recordset.columns[key].type.name];
    });
  }
};