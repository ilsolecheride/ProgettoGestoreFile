﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WPFPageSwitch
{

    //Modella una lista di FileUtente
    class FileUtenteList : DB_Table, IEnumerable
    {
        //Attributi
        private System.Collections.Generic.List<int> __list_ids_files;
        private FileUtente[] __file_list;
        static private string sql_get_file_ids = Properties.SQLquery.sqlGetId;

        //Proprieta
        public int Length => __list_ids_files.Count;

        public FileUtente this[int index] 
        {
            get
            {
                if(__file_list[index] == null)
                {
                    __file_list[index] = new FileUtente(__list_ids_files[index]);
                }
                return __file_list[index];
            }
           // set { }
        }

        public FileUtente this[string nome_file, string path_file]
        {
            get
            {
                for(int i = 0; i < this.__list_ids_files.Count; i++)
                {
                    if (this[i].Nome == nome_file && this[i].Path == path_file)
                        return this[i];
                }
                throw new DatabaseException(" Non esiste nessun file con questo nome.", DatabaseErrorCode.FileNonEsistente);
            }
        }

        //Costruttori
        public FileUtenteList(): base()
        {
            //this.__max_file = Properties.Settings.Default.numero_file;
            this.__list_ids_files = new System.Collections.Generic.List<int>();
            this.ExecuteQuery(sql_get_file_ids, null);
            //Get the data
            foreach (int i in this.GetResults())
            {
                this.__list_ids_files.Add(Int32.Parse(this.ResultGetValue("id").ToString()));
            }
            this.__file_list = new FileUtente[this.__list_ids_files.Count];
        }
        /// <summary>
        /// Restituisce tutti i file in una cartella, scendendo ricorsivamente nelle sottocartelle
        /// </summary>
        /// <param name="rootFolderPath">Indica il path base, scritto con il separatore alla fine</param>
        /// <returns>
        /// Un array di coppie nome file - percorso, dove il percorso è da intendersi a partire da rootFolderPath
        /// </returns>

        static public List<string[]> exploreFileSystem(string rootFolderPath)
        {
            Queue pending = new Queue();
            pending.Enqueue(rootFolderPath);
            List<string[]> files = new List<string[]>();
            string[] tmp;
            string[] f_info = new string[2];
            int index = 0;

            while (pending.Count > 0)
            {
                rootFolderPath = (string)pending.Dequeue();
                tmp = Directory.GetFiles(rootFolderPath);
                for (int i = 0; i < tmp.Length; i++)
                {
                    f_info[0] = Path.GetFileName(tmp[i]);
                    f_info[1] = Path.GetDirectoryName(tmp[i]);
                    index = f_info[1].IndexOf(rootFolderPath);
                    f_info[1] = (index < 0) ?
                        f_info[1] : f_info[1].Remove(index, rootFolderPath.Length);

                    files.Add( f_info);
                }
                tmp = Directory.GetDirectories(rootFolderPath);
                for (int i = 0; i < tmp.Length; i++)
                {
                    pending.Enqueue(tmp[i]);
                }
            }
            return files;
        }

        //Distruttore
        //Metodi

        /// <summary>
        ///     Usata per ciclare sui file di un utente.
        ///     Non carica tutti i file in una volta
        /// </summary>
        /// <returns>
        ///     Un iteratore per il costrutto "foreach".
        /// </returns>
        public IEnumerator GetEnumerator()
        {
            int index;
            for (index = 0; index < this.__list_ids_files.Count; index++)
            {
                yield return this[index];
            }
        }
        //Metodi Statici
    }
}
