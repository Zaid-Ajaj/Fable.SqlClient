const sql = require("mssql");

const columnDefinitions = result => {
  var keys = Object.keys(result.recordset.columns);
  return keys.map(key => {
    return [key, result.recordset.columns[key].type.name];
  });
}


sql.connect({ 
    user: "Dev",
    server: "10.8.0.1", 
    password: "49BfQdya91jo", 
    database: "master"
}, function(err) {
    sql.query("select CAST('2007-05-08 12:35:29.1234567+12:15' AS datetimeoffset(7)) as [time], cast(1 as tinyint) as [Tiny], cast(1 as smallint) as [Small], cast(1 as money) as [Money], newid() as [guid], cast(1 as bit) as [boolean], cast(1 as bigint) as [Big]", function(err, result) {
        if (err) {
            console.error(err);
            return;
        }
        
        console.dir(result);
        console.dir(result.recordsets[0][0].time);
        console.log(result.recordsets[0][0].time instanceof Date)
        console.dir(result.recordset);
        console.log(columnDefinitions(result));
    })
})