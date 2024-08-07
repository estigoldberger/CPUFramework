using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;

namespace CPUFramework
{
    public class SQLUtility
    {
        public static string ConnectionString = "";
        public static SqlCommand GetSqlCommand(string sprocname)
        {
            SqlCommand cmd;
            using SqlConnection conn = new SqlConnection(ConnectionString);
            {
                cmd= new SqlCommand(sprocname, conn);
                cmd.CommandType = CommandType.StoredProcedure;
                conn.Open();
                SqlCommandBuilder.DeriveParameters(cmd);
            }
            return cmd;
        }
    

        public static DataTable GetDataTable(SqlCommand cmd)
        {
            return DoExecuteSql(cmd, true);
        }
        public static void SaveDataTable(DataTable dt, string sprocname)
        {
         var rows=   dt.Select("", "", DataViewRowState.Added | DataViewRowState.ModifiedCurrent);
            foreach(DataRow r in rows)
            {
                SaveDataRow(r, sprocname, false);
            }
            dt.AcceptChanges();
        }
        public static void SaveDataRow(DataRow dr, string sprocname, bool acceptchanges=true)
        {
            SqlCommand cmd = GetSqlCommand(sprocname);
            foreach(DataColumn col in dr.Table.Columns )
            {
                string paramname = $"@{col.ColumnName}";
                if (cmd.Parameters.Contains(paramname))
                {
                    cmd.Parameters[paramname].Value = dr[col.ColumnName];
                }
            }
            DoExecuteSql(cmd, false);
            
            foreach(SqlParameter sp in cmd.Parameters)
            {
                if (sp.Direction== ParameterDirection.InputOutput)
                {
                    string columname = sp.ParameterName.Substring(1);
                    if (dr.Table.Columns.Contains(columname))
                    {
                        dr[columname] = sp.Value;
                    }
                }

            }
            if (acceptchanges == true)
            {
                dr.Table.AcceptChanges();
            }
           
        }
        private static DataTable DoExecuteSql(SqlCommand cmd, bool loadtable )
        {
            
            DataTable dt = new();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                cmd.Connection = conn;
                Debug.Print(GetSQL(cmd));
                try
                {
                    SqlDataReader dr = cmd.ExecuteReader();
                    CheckReturnValue(cmd);
                    if (loadtable == true)
                    {
                        dt.Load(dr);
                    }
                }
                catch (SqlException ex)
                {
                    string msg = ParseConstraintMsg(ex.Message);
                    throw new Exception(msg);
                }
                catch (InvalidCastException ex)
                {
                    throw new Exception(cmd.CommandText + ": " + ex.Message, ex);
                }
                
            }
            SetAllColumnsProperties(dt);
            return dt;
        }
        private static void CheckReturnValue(SqlCommand cmd)
        {
            int returnvalue = 0;
            string msg = " ";
            if (cmd.CommandType == CommandType.StoredProcedure)
            {


                foreach (SqlParameter p in cmd.Parameters)
                {
                    if (p.Direction == ParameterDirection.ReturnValue)
                    {
                        if (p.Value != null)
                        {
                            returnvalue = (int)p.Value;
                        }
                    }
                    else if (p.ParameterName.ToLower() == "@message")
                    {
                        if (p.Value != null)
                        {

                            msg = p.Value.ToString();
                        }
                    }
                }
                if (returnvalue == 1)
                {
                    if (msg == " ")
                    {
                        msg = cmd.CommandText + " did not do the action that was requested";
                    }
                    throw new Exception(msg);
                }
            }
        }  
        
        public static DataTable GetDataTable(string sqlstatement)
        {
            
            return DoExecuteSql(new SqlCommand(sqlstatement), true);
        }
        public static void ExecuteSQL(string sqlstatement)
        {
            GetDataTable(sqlstatement);
        }
        public static void ExecuteSQL(SqlCommand cmd)
        {
            DoExecuteSql(cmd, false);
        }

        public static int GetFirstColumnFirstRowValue(string sql)
        {
            int n = 0;
            DataTable dt = GetDataTable(sql);
            if (dt.Rows.Count > 0 && dt.Columns.Count > 0)
            {
                if (dt.Rows[0][0] != DBNull.Value)
                {
                    
                    int.TryParse(dt.Rows[0][0].ToString(), out n);
                }
            }
            return n;
        }
        public static void SetAllColumnsProperties(DataTable dt)
        {
            foreach(DataColumn c in dt.Columns)
            {
                c.AllowDBNull = true;
                c.AutoIncrement = false;
            }
        }
        public static bool TableHasChanges(DataTable dt)
        {
            bool b = false;
            if(dt.GetChanges() != null)
            {
                b = true;
            }
            return b;
        }
        public static string GetSQL(SqlCommand cmd)
        {
            string val = "";
#if DEBUG
            StringBuilder sb = new();
            if(cmd.Connection != null)
            {
                sb.AppendLine($"---{cmd.Connection.DataSource}");
                sb.AppendLine($"use {cmd.Connection.Database}");
                sb.AppendLine("Go");
            }
            if (cmd.CommandType == CommandType.StoredProcedure)
            {

                sb.AppendLine($"exec {cmd.CommandText}");
                int paramcount = cmd.Parameters.Count - 1;
                int paramnum = 0;
                string comma = ",";
                foreach(SqlParameter p in cmd.Parameters)
                {
                    if (p.Direction != ParameterDirection.ReturnValue)
                    {
                        if (paramnum == paramcount)
                        {
                            comma = "";
                        }
                        sb.AppendLine($"{p.ParameterName}= {(p.Value == null ? "Null" : p.Value.ToString())}{comma}");
                       
                    }
                    paramnum++;
                }
            }
            else
            {
                sb.AppendLine(cmd.CommandText);
            }
            val = sb.ToString();
#endif
            return val;
        }
        private static string ParseConstraintMsg(string msg)
        {
            string origmsg = msg;
            string prefix = "ck_";
            string msgend = "";
            string notnullprefix = "Cannot insert the value NULL into column '";
            if (msg.Contains(prefix) == false)
            {
                if (msg.Contains("u_") )
                {
                    if (msg.Contains("_must_be_unique"))
                    {

                        prefix = "u_";

                    }
                    else
                    {
                        prefix = "u_";
                        msgend = " must be unique";
                    }
                }
                else if (msg.Contains("f_") )
                {
                    prefix = "f_";
                    
                
                }
                else if (msg.Contains(notnullprefix))
                {
                    prefix = notnullprefix;
                    msgend = " cannot be blank.";
                }
            }
            if (msg.Contains(prefix))
            {
                msg = msg.Replace("\"", "'");
                int pos = msg.IndexOf(prefix) + prefix.Length;
                msg = msg.Substring(pos);
                pos = msg.IndexOf("'");
                if (pos == -1)
                {
                    msg = origmsg;
                }
                else
                {
                    msg = msg.Substring(0, pos);
                    msg = msg.Replace("_", " ");
                    msg = msg + msgend;
                   
                    if(prefix == "f_")
                    {
                        var words = msg.Split(" ");
                        if(words.Length > 1)
                        {
                            msg = $"Cannot delete {words[0]} because it has a related {words[1]} record.";
                        }
                    }
                }
            }
            return msg;
        }
        public static void SetParameterValue(SqlCommand cmd, string paramname, object value)
        {
            if (paramname.StartsWith("@") == false)
            {
                paramname = "@"+ paramname ;
            }
            try
            {
                cmd.Parameters[paramname].Value = value;
            }
            catch (Exception ex)
            {
                throw new Exception(cmd.CommandText + ": " + ex.Message, ex);
            }
        }
        public static int GetValueFromFirstRowAsInt(DataTable tbl, string columname)
        {
            int value = 0;
            if(tbl.Rows.Count > 0)
            {
                DataRow r = tbl.Rows[0];
                if (r[columname] != null && r[columname] is int)
                {
                    value = (int)r[columname];
                }

            }
            return value;
        }
        public static string GetValueFromFirstRowAsString(DataTable tbl, string columname)
        {
            string value = "";
            if (tbl.Rows.Count > 0)
            {
                DataRow r = tbl.Rows[0];
                if (r[columname] != null && r[columname] is string)
                {
                    value =(string)r[columname];
                }

            }
            return value;
        }
        public static void DebugPrintDataTable (DataTable dt)
        {
            foreach(DataRow r in dt.Rows)
            {
                foreach(DataColumn c in dt.Columns)
                {
                    Debug.Print(c.ColumnName+ "+" + r[c.ColumnName].ToString());
                }
            }
        }
    }
}
