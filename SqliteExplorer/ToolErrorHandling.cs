using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NLog.Targets;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlTypes;

namespace SqliteExplorer
{
    public class ToolErrorHandling
    {
        public static string HandleToolError(SqliteException exception)
        {
            // Implement error handling here
            return $"Error {exception.ErrorCode}: {exception.Message}";
        }
    }
}
