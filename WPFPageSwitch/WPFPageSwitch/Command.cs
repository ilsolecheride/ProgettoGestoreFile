﻿using System;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace WPFPageSwitch
{
    /// <summary>
    /// Classe che gestisce il collegamento di un client, i comandi, e lo scambio dei file.
    /// </summary>
    /// <example>
    /// Formato messaggi client:
    /// comando\r\n
    /// dati\r\n
    /// ...
    /// \r\n (linea vuota)
    /// Formato risposte server:
    /// CommandErrorCode Messaggio\r\n
    /// dati\r\n
    /// ...
    /// \r\n
    /// </example>
    abstract class Command
    {
        Log l;
        static protected TcpClient s;
        static protected bool __logged = false;
        static private bool __connected = false;
        static protected string base_path = Properties.Settings.Default.base_path;
        protected StreamReader control_stream_reader = null;
        protected StreamWriter control_stream_writer = null;
        protected NetworkStream data_stream = null;
        private IPAddress server_addr;
        private int server_port;

        public Command()
        {
            l = Log.getLog();

            if (s != null && s.Connected)
            {
                __connected = true;
            }
            else
            {
                server_addr = IPAddress.Parse(Properties.Settings.Default.ip_address);
                server_port = Properties.Settings.Default.port;

                s = new TcpClient();
                try
                {
                    s.Connect(server_addr, server_port);
                    __connected = true;
                    control_stream_reader = new StreamReader(s.GetStream(),Encoding.ASCII);
                    control_stream_writer = new StreamWriter(s.GetStream(),Encoding.ASCII);
                }
                catch (Exception e)
                {
                    l.log(e.Message, Level.ERR);
                    throw new ClientException("Errore di connessione: " + e.Message, ClientErrorCode.ServerNonDisponibile);
                }
            }
        }
        //Proprietà
        /// <summary>
        /// Se falso, l'oggetto più essere distrutto
        /// </summary>
        static public bool Connected
        {
            get { return __connected; }
        }
        static public bool Logged { get { return __logged; } }
        abstract public void esegui();
    }

    class ComandoRegistra : Command
    {
        string nome_utente, password;
        const string nome_comando = "REGISTER";

        public ComandoRegistra(string nome_utente, string password):
            base()
        {
            this.nome_utente = nome_utente;
            this.password = password;
        }
        /// <summary>
        /// Registra un nuovo utente
        /// </summary>
        /// <exception>CommandExeption con un codice corrispondente all'errore riscontrato</exception>
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(nome_utente).Append(Environment.NewLine).
                Append(password).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    __logged = true;
                    break;
                case CommandErrorCode.NomeUtenteInUso:
                    throw new ServerException(Properties.Messaggi.nomeUtenteInUso,ServerErrorCode.NomeUtenteInUso);
                case CommandErrorCode.FormatoDatiErrato:
                    throw new ServerException(Properties.Messaggi.formatoDatiErrato,ServerErrorCode.FormatoDatiErrato);
                case CommandErrorCode.MomentoSbagliato:
                    throw new ServerException(Properties.Messaggi.momentoSbagliato,ServerErrorCode.MomentoSbagliato);
                case CommandErrorCode.DatiIncompleti:
                default:
                    throw new ServerException(Properties.Messaggi.erroreServer,ServerErrorCode.Default);
            }
        }
    }

    class ComandoLogin : Command
    {
        string nome_utente, password;
        const string nome_comando = "LOGIN";

        public ComandoLogin(string nome_utente, string password):
            base()
        {
            this.nome_utente = nome_utente;
            this.password = password;
        }
        /// <summary>
        /// Identifica un utente con il server. E' necessario che sia il primo comando ogni nuova connessione
        /// </summary>
        /// <exception>CommandExeption con un codice corrispondente all'errore riscontrato</exception>
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(nome_utente).Append(Environment.NewLine).
                Append(password).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    __logged = true;
                    break;
                case CommandErrorCode.FormatoDatiErrato:
                    throw new ServerException(Properties.Messaggi.formatoDatiErrato, ServerErrorCode.FormatoDatiErrato);
                case CommandErrorCode.MomentoSbagliato:
                    throw new ServerException(Properties.Messaggi.momentoSbagliato, ServerErrorCode.MomentoSbagliato);
                case CommandErrorCode.DatiIncompleti:
                default:
                    throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
        }
    }

    class ComandoNuovoFile : Command
    {
        string path;
        string nome_file;
        string path_completo;
        int dim;
        string sha_contenuto;
        DateTime t_creazione;
        FileStream file = null;
        const string nome_comando = "NEWFILE";

        public ComandoNuovoFile(string nome_file, string path, int dim = -1, 
                                DateTime t_creazione = new DateTime(), string sha_contenuto = null
                                )
            : base()
        {
            FileInfo finfo = null;
            Log l = Log.getLog();
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }
            this.path = path;
            this.nome_file = nome_file;
            this.path_completo = new StringBuilder(base_path).Append(Path.DirectorySeparatorChar).Append(path).Append(Path.DirectorySeparatorChar).Append(nome_file).ToString();

            try
            {
                finfo = new FileInfo(path_completo);
                if (!finfo.Exists)
                {
                    throw new Exception("Il file da inviare non esiste.");
                }
            }
            catch(Exception e)
            {
                    throw new Exception("Errore nel leggere i parametri del file. Forse i parametri sono sbagliati. "+e.Message);
            }
            if(dim < 0)
                dim = (int)(finfo.Length);
            if(t_creazione == DateTime.MinValue)
                t_creazione = finfo.CreationTime;

            file = File.Open(this.path_completo,FileMode.Open);
            if (sha_contenuto == null)
            {
                SHA256 sha_obj = SHA256Managed.Create();
                byte[] hash_val;
                hash_val = sha_obj.ComputeHash(this.file);
            }
            this.file.Position = 0;
        }
        /// <summary>
        /// Comando per la creazione di un nuovo file sul server. Se l'utente ha troppi file, il più
        /// vecchio tra quelli eliminati viene distrutto. Se non ci sono file eliminati da distruggere
        /// viene generato un errore.
        /// </summary>
        /// <exception>CommandExeption con un codice corrispondente all'errore riscontrato</exception>
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(nome_file).Append(Environment.NewLine).
                Append(path).Append(Environment.NewLine).
                Append(t_creazione.Ticks).Append(Environment.NewLine).
                Append(sha_contenuto).Append(Environment.NewLine).
                Append(dim).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OKIntermedio:
                    try
                    {
                        data_stream = CollegamentoDati.getCollegamentoDati(control_stream_reader.ReadLine());
                    }
                    catch
                    {
                        throw new ServerException(Properties.Messaggi.collegamentoDati,
                            ServerErrorCode.CollegamentoDatiNonDisponibile);
                    }
                    break;
                case CommandErrorCode.FormatoDatiErrato:
                    throw new ServerException(Properties.Messaggi.formatoDatiErrato, ServerErrorCode.FormatoDatiErrato);
                case CommandErrorCode.UtenteNonLoggato:
                    throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
                case CommandErrorCode.FileEsistente:
                    throw new ServerException(Properties.Messaggi.fileEsistente, ServerErrorCode.FileEsistente);
                case CommandErrorCode.LimiteFileSuperato:
                    throw new ServerException(Properties.Messaggi.limiteFileSuperato, ServerErrorCode.LimiteFileSuperato);
                case CommandErrorCode.DatiIncompleti:
                default:
                    throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
            byte[] buffer = new byte[1024];
            int size = 1024;
            try
            {
                while ((size = file.Read(buffer, 0, size)) != 0)
                {
                    data_stream.Write(buffer, 0, size);
                }
            }
            catch
            {
                throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
        }

        ~ComandoNuovoFile()
        {
            data_stream.Close();
            file.Close();
        }
    }

    class ComandoScaricaFile : Command
    {
        string path;
        string nome_file;
        string path_completo;
        string tmp_path;
        int dim;
        string sha_contenuto;
        DateTime t_creazione;
        FileStream file,tmp_file;
        const string nome_comando = "RETRIEVE";

        public ComandoScaricaFile(string nome_file, string path, DateTime timestamp)
            : base()
        {
            Log l = Log.getLog();
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }
            this.path = path;
            this.nome_file = nome_file;
            this.path_completo = new StringBuilder(path).Append(path).Append(Path.DirectorySeparatorChar).Append(nome_file).ToString();
            tmp_path = Path.GetTempFileName();
            tmp_file = File.Open(this.tmp_path, FileMode.Open);

        }

        /// <summary>
        /// Carica la versione richiesta del file all'utente che lo richiede.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="dati">
        ///     [0]: nome_file
        ///     [1]: path relativo
        ///     [2]: timestamp versione (in Ticks)
        /// </param>
        /// <returns>
        /// OK intermedio
        /// token
        /// dimensione file
        /// sha contenuto
        /// </returns>
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(nome_file).Append(Environment.NewLine).
                Append(path).Append(Environment.NewLine).
                Append(t_creazione.Ticks).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OKIntermedio:
                    try
                    {
                        string token = control_stream_reader.ReadLine();
                        this.dim = Int32.Parse(control_stream_reader.ReadLine());
                        this.sha_contenuto = control_stream_reader.ReadLine();
                        data_stream = CollegamentoDati.getCollegamentoDati(token);
                    }
                    catch
                    {
                        throw new ServerException(Properties.Messaggi.collegamentoDati,
                            ServerErrorCode.CollegamentoDatiNonDisponibile);
                    }
                    break;
                case CommandErrorCode.FormatoDatiErrato:
                    throw new ServerException(Properties.Messaggi.formatoDatiErrato, ServerErrorCode.FormatoDatiErrato);
                case CommandErrorCode.UtenteNonLoggato:
                    throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
                case CommandErrorCode.AperturaFile:
                    throw new ServerException(Properties.Messaggi.fileEsistente, ServerErrorCode.FileEsistente);
                case CommandErrorCode.DatiIncompleti:
                default:
                    throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
            byte[] buffer = new byte[1024];
            int size = 1024;
            int tot_read = 0;
            try
            {
                while ((size = data_stream.Read(buffer, 0, size)) != 0)
                {
                    //Scrivo su un file temporaneo. Se tutto va bene sostituisco quello presente
                    //nella cartella dell'utente
                    tot_read += size;
                    tmp_file.Write(buffer, 0, size);
                }
            }
            catch
            {
                throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
            finally
            {
                tmp_file.Close();
                data_stream.Close();
            }
            
            if (tot_read != this.dim)
            {
                throw new ServerException(Properties.Messaggi.datiInconsistenti, ServerErrorCode.DatiInconsistenti);
            }
            SHA256 sha_obj = SHA256Managed.Create();
            byte[] hash_val;
            hash_val = sha_obj.ComputeHash(File.Open(tmp_path,FileMode.Open));
            StringBuilder hex = new StringBuilder(hash_val.Length * 2);
            foreach (byte b in hash_val)
                hex.AppendFormat("{0:x2}", b);
            string sha_reale = hex.ToString();
            if(sha_reale != sha_contenuto.Trim())
            {
                throw new ServerException(Properties.Messaggi.datiInconsistenti, ServerErrorCode.DatiInconsistenti);
            }
            try
            {
                File.Delete(path_completo);
                File.Move(tmp_path, path_completo);
            }catch(Exception e)
            {
                throw new DatabaseException("Il file scaricato non può essere memorizzato nella cartella di destinazione. Controllare i permessi e riprovare. "+e.Message, DatabaseErrorCode.Unknown);
            }
            try
            {
                File.Delete(tmp_path);
            }
            catch {; }
        }
    }

    class ComandoAggiornaContenutoFile : Command
    {
        string path;
        string nome_file;
        string path_completo;
        int dim;
        string sha_contenuto;
        DateTime t_modifica;
        FileStream file = null;
        const string nome_comando = "UPDATE";

        public ComandoAggiornaContenutoFile(string nome_file, string path, int dim = -1,
                                DateTime t_modifica = new DateTime(), string sha_contenuto = null
                                )
            : base()
        {
            FileInfo finfo = null;
            Log l = Log.getLog();
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }
            this.path = path;
            this.nome_file = nome_file;
            this.path_completo = new StringBuilder(base_path).Append(Path.DirectorySeparatorChar).Append(path).Append(Path.DirectorySeparatorChar).Append(nome_file).ToString();

            try
            {
                finfo = new FileInfo(path_completo);
                if (!finfo.Exists)
                {
                    throw new Exception("Il file da inviare non esiste.");
                }
            }
            catch (Exception e)
            {
                throw new Exception("Errore nel leggere i parametri del file. Forse i parametri sono sbagliati. " + e.Message);
            }
            if (dim < 0)
                dim = (int)(finfo.Length);
            if (t_modifica == DateTime.MinValue)
                t_modifica = finfo.LastWriteTime;

            file = File.Open(this.path_completo, FileMode.Open);
            if (sha_contenuto == null)
            {
                SHA256 sha_obj = SHA256Managed.Create();
                byte[] hash_val;
                hash_val = sha_obj.ComputeHash(this.file);
                this.sha_contenuto = System.Convert.ToBase64String(hash_val);
            }
            this.file.Position = 0;
        }
        /// <summary>
        /// Comando usato per aggiornare il contenuto di un file sul server.
        /// </summary>
        /// <exception>ServerExeption con un codice corrispondente all'errore riscontrato</exception>
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(nome_file).Append(Environment.NewLine).
                Append(path).Append(Environment.NewLine).
                Append(t_modifica.Ticks).Append(Environment.NewLine).
                Append(sha_contenuto).Append(Environment.NewLine).
                Append(dim).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OKIntermedio:
                    try
                    {
                        data_stream = CollegamentoDati.getCollegamentoDati(control_stream_reader.ReadLine());
                    }
                    catch
                    {
                        throw new ServerException(Properties.Messaggi.collegamentoDati,
                            ServerErrorCode.CollegamentoDatiNonDisponibile);
                    }
                    break;
                case CommandErrorCode.FormatoDatiErrato:
                    throw new ServerException(Properties.Messaggi.formatoDatiErrato, ServerErrorCode.FormatoDatiErrato);
                case CommandErrorCode.UtenteNonLoggato:
                    throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
                case CommandErrorCode.FileEsistente:
                    throw new ServerException(Properties.Messaggi.fileEsistente, ServerErrorCode.FileEsistente);
                case CommandErrorCode.LimiteFileSuperato:
                    throw new ServerException(Properties.Messaggi.limiteFileSuperato, ServerErrorCode.LimiteFileSuperato);
                case CommandErrorCode.DatiIncompleti:
                default:
                    throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
            byte[] buffer = new byte[1024];
            int size = 1024;
            try
            {
                while ((size = file.Read(buffer, 0, size)) != 0)
                {
                    data_stream.Write(buffer, 0, size);
                }
            }
            catch
            {
                throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
            //Leggo la risposta (se tutto è andato bene o c'è stato un errore)
            response = control_stream_reader.ReadLine();
            response = response.Trim();
            errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    
                    break;
                default:
                    throw new ServerException(Properties.Messaggi.erroreServer, ServerErrorCode.Default);
            }
        }

        ~ComandoAggiornaContenutoFile()
        {
            data_stream.Close();
            file.Close();
        }
    }

    class ComandoEliminaFile : Command
    {
        string path;
        string nome_file;
        string path_completo;
        const string nome_comando = "DELETE";

        public ComandoEliminaFile(string nome_file, string path)
            : base()
        {
            Log l = Log.getLog();
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }
            this.path = path;
            this.nome_file = nome_file;
            this.path_completo = new StringBuilder(base_path).Append(Path.DirectorySeparatorChar).Append(path).Append(Path.DirectorySeparatorChar).Append(nome_file).ToString();
        }
        /// <summary>
        /// Setta un file come non valido. Esiste ancora.
        /// </summary>
        /// <returns></returns>
        public override void esegui()
        {
            Log l = Log.getLog();
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(nome_file).Append(Environment.NewLine).
                Append(path).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    l.log(String.Format("File eliminato con successo: %s", path + Path.DirectorySeparatorChar + nome_file), Level.INFO);
                    break;
                default:
                    throw new ServerException();
            }
        }
    }

    class ComandoListFolders : Command
    {
        System.Collections.Generic.List<string> __paths = null;
        const string nome_comando = "LISTPATHS";

        string[] Paths
        {
            get { return __paths.ToArray(); }
        }
        public ComandoListFolders() : base()
        {
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }
            __paths = new System.Collections.Generic.List<string>();

        }

        public override void esegui()
        {
            StringBuilder sb = new StringBuilder().Append(nome_comando).Append(Environment.NewLine)
                .Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    break;
                default:
                    throw new ServerException();
            }
            //Finché non c'è una riga vuota
            while((response = control_stream_reader.ReadLine().Trim()).Length > 0)
            {
                __paths.Add(response);
            }
        }
    }

    class ComandoListDir : Command
    {
        System.Collections.Generic.List<string> __files;
        string path;
        const string nome_comando = "LISTDIR";
        
        string[] FileNames
        {
            get { return this.__files.ToArray(); }
        }

        public ComandoListDir(string path) : base()
        {
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }

            this.path = path;
            __files = new System.Collections.Generic.List<string>();
        }
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder().Append(nome_comando).Append(Environment.NewLine)
                .Append(path).Append(Environment.NewLine)
                .Append(Environment.NewLine);

            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    break;
                case CommandErrorCode.DatiIncompleti:
                case CommandErrorCode.DatiErrati:
                    throw new ServerException("I dati forniti dall'utente non sono corretti.", ServerErrorCode.DatiIncompleti);
                default:
                    throw new ServerException();
            }
            //Finché non c'è una riga vuota
            while ((response = control_stream_reader.ReadLine().Trim()).Length > 0)
            {
                __files.Add(response);
            }
        }
    }

    class ComandoListVersions : Command
    {
        System.Collections.Generic.List<DateTime> __versions;
        string path;
        string nome_file;
        const string nome_comando = "LISTVERSIONS";

        DateTime[] Versions
        {
            get { return __versions.ToArray(); }
        }

        public ComandoListVersions(string nome_file, string path) : base()
        {
            if (!Logged)
            {
                throw new ServerException(Properties.Messaggi.nonLoggato, ServerErrorCode.UtenteNonLoggato);
            }

            this.path = path;
            this.nome_file = nome_file;
            __versions = new System.Collections.Generic.List<DateTime>();
        }

        public override void esegui()
        {
            StringBuilder sb = new StringBuilder().Append(nome_comando).Append(Environment.NewLine)
                .Append(path).Append(Environment.NewLine)
                .Append(Environment.NewLine);

            control_stream_writer.Write(sb.ToString());
            string response = control_stream_reader.ReadLine();
            response = response.Trim();
            CommandErrorCode errorCode = (CommandErrorCode)Int32.Parse(response.Split(' ')[0]); //Extract code from response
            switch (errorCode)
            {
                case CommandErrorCode.OK:
                    break;
                case CommandErrorCode.DatiIncompleti:
                case CommandErrorCode.DatiErrati:
                    throw new ServerException("I dati forniti dall'utente non sono corretti.", ServerErrorCode.DatiIncompleti);
                default:
                    throw new ServerException();
            }
            //Finché non c'è una riga vuota
            while ((response = control_stream_reader.ReadLine().Trim()).Length > 0)
            {
                __versions.Add(new DateTime(Int64.Parse(response)));
            }

        }
    }

    class ComandoEsci : Command
    {
        const string nome_comando = "EXIT"; 
        public override void esegui()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nome_comando).Append(Environment.NewLine).
                Append(Environment.NewLine);
            control_stream_writer.Write(sb.ToString());
            this.control_stream_reader.Close();
            this.control_stream_writer.Close();
           
        }
    }

}
