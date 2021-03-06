﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WPFPageSwitch
{
    class FileUtente: DB_Table
    {
        private int id;
        private string __nome_file_c;
        private string __path_relativo_c;
        private string __path_completo;
        private DateTime __t_creazione;
        private DateTime __t_modifica;
        private int __dim;
        private string sha_contenuto;
        private Log l;
        static private string sql_get_file_data = Properties.SQLquery.sqlGetFileData;

        //Proprietà
        public string Nome
        {
            get{ return __nome_file_c; }
            set
            {
                __nome_file_c = value;
            }
        }
        public string Path
        {
            get { return __path_relativo_c; }
            set { __path_relativo_c = value; }
        }
        public DateTime TempoCreazione => __t_creazione;
        //Modificabili solo dalla funzione di aggiornamento
        public DateTime TempoModifica => __t_modifica;
        public int Dimensione => __dim;
        public string SHA256Contenuto
        {
            get
            {
                if (sha_contenuto == null)
                    sha_contenuto = FileUtente.CalcolaSHA256(File.Open(this.__path_completo, FileMode.Open));
                return sha_contenuto;
            }
        }

        public FileUtente(int id): base()
        {
            l = Log.getLog();
            string[][] parameters = new string[1][];

            parameters[0] = new string[2] { "@id", id.ToString() };
            this.ExecuteQuery(sql_get_file_data, parameters);
            //Get the data
            foreach (Int32 i in GetResults())
            {
                this.__nome_file_c = (string)(this.ResultGetValue("nome_file_c"));
                this.__t_creazione = (DateTime)(this.ResultGetValue("t_creazione"));
                this.__path_relativo_c = (string)(this.ResultGetValue("path_relativo_c"));
                this.__t_modifica = (DateTime)(this.ResultGetValue("t_modifica"));
                this.__dim = Int32.Parse((string)(this.ResultGetValue("dim")));
                this.sha_contenuto = (string)(this.ResultGetValue("sha_contenuto"));
            }
            this.id = id;
            this.__path_completo = Properties.Settings.Default.base_path + System.IO.Path.DirectorySeparatorChar + this.Path +
                                    System.IO.Path.DirectorySeparatorChar + this.Nome;
        }
        
        static public string CalcolaSHA256(FileStream f)
        {
            SHA256 sha_obj = SHA256Managed.Create();
            byte[] hash_val;
            f.Position = 0;
            hash_val = sha_obj.ComputeHash(f);
            f.Close();
            StringBuilder hex = new StringBuilder(hash_val.Length * 2);
            foreach (byte b in hash_val)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public void aggiornaDati(int newDim, DateTime newModTime, string newHash = null)
        {
            this.__dim = newDim;
            this.__t_modifica = newModTime;
            if(newHash == null)
            {
                newHash = FileUtente.CalcolaSHA256(File.Open(this.__path_completo, FileMode.Open));
            }
            this.sha_contenuto = newHash;
            ///TODO: Memorizzare sul db
            string[][] parameters = new string[4][];

            parameters[0] = new string[2] { "@dim", __dim.ToString() };
            parameters[1] = new string[2] { "@t_modifica", __t_modifica.ToString() };
            parameters[2] = new string[2] { "@sha_contenuto", sha_contenuto };
            parameters[3] = new string[2] { "@id", id.ToString() };
            this.ExecuteQuery(sql_get_file_data, parameters);
        }

        public void Delete()
        {
            throw new NotImplementedException();
        }

        static public FileUtente CreaNuovo(string nome_file, string path, DateTime t_creazione, int dim, string sha_contenuto = null)
        {
            int id = 0;
            DB_Table db = new DB_Table();
            sha_contenuto = sha_contenuto != null ? sha_contenuto : "";
            string[][] parameters = new string[6][];

            parameters[0] = new string[2] { "@dim", dim.ToString() };
            parameters[1] = new string[2] { "@t_modifica", t_creazione.ToString() };
            parameters[2] = new string[2] { "@t_creazione", t_creazione.ToString() };
            parameters[3] = new string[2] { "@sha_contenuto", sha_contenuto };
            parameters[4] = new string[2] { "@nome_file", nome_file };
            parameters[5] = new string[2] { "@path", path };

            db.ExecuteQuery(sql_get_file_data, parameters);
            id = (int)db.getLastInsertedId();
            return new FileUtente(id);
        }
    }
}