﻿using System.Data;
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
        public static void SaveDataRow(DataRow dr, string sprocname)
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
            SetAllColumnsAllowNull(dt);
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
        public static void SetAllColumnsAllowNull(DataTable dt)
        {
            foreach(DataColumn c in dt.Columns)
            {
                c.AllowDBNull = true;
            }
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
            try
            {
                cmd.Parameters[paramname].Value = value;
            }
            catch (Exception ex)
            {
                throw new Exception(cmd.CommandText + ": " + ex.Message, ex);
            }
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
