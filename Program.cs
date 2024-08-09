using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using TOps.BL;
using TOps.Repository;

namespace ConsoleComparacaoDJEN
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Executando o Console...");

            var fontesDiponiveis = FontesDisponiveis();
            Console.WriteLine($"Fontes Disponíveis para Check de Comparação - Quantidades de Diários disponíveis para Finalização: {fontesDiponiveis}");
            Console.WriteLine("");
            Console.WriteLine("");

            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string updateQuery = "UPDATE [TLEGAL0].[dbo].[ConsoleDownloadDJEN_D+] SET STATUS = 2 WHERE STATUS = 1 AND publicacoescarregadas = 0;";
                SqlCommand command = new SqlCommand(updateQuery, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 0;
                connection.Open();
                command.ExecuteNonQuery();
                connection.Close();
            }

            if (fontesDiponiveis > 0)
            {
                List<Fontes> ListaFontes = CarregarFontes();

                var novasPublicacoes = new List<NovaPublicacao>();

                foreach (var l in ListaFontes)
                {
                    var idfonte = l.IdFonte;
                    var data = l.Data;
                    var iduf = l.IdUf;
                    var sigla = l.Sigla;
                    var uf = l.Uf;
                    var fonte = l.Fonte;
                    var iO = l.IO;

                    DateTime dataFormatada1 = DateTime.ParseExact(data, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    string dataFormatadaStringPasta_ = dataFormatada1.ToString("yyyyMMdd");

                    var arquivoFonte = SearchPath(data, iduf, sigla, uf, idfonte);

                    if (arquivoFonte == string.Empty)
                    {
                        Console.WriteLine($"Sem arquivo para comparar... {sigla}");

                        var newFileName = $"{iO}_{DateTime.Parse(data).ToString("ddMMyyyy")}.txt";

                        string folderPathFonteD = $"T:\\IO\\djen\\{dataFormatadaStringPasta_}\\D+";
                        string folderPathFonte = $"T:\\IO\\djen\\{dataFormatadaStringPasta_}";
                        
                        if (!Directory.Exists($"{folderPathFonte}\\Suplemento"))
                        {
                            Directory.CreateDirectory($"{folderPathFonte}\\Suplemento");
                        }

                        File.Move($"{folderPathFonteD}\\{newFileName}", $"{folderPathFonte}\\Suplemento\\{newFileName}");

                        string dateDiario = DateTime.Parse(data).ToString("yyyy-MM-dd");

                        ExecuteCommand($"insert into [TLEGAL0].[dbo].[ConsoleDownloadDJEN] ([idfonte], [iduf], [sigla], [uf], [fonte], [data], [status]) values ({idfonte}, {iduf}, '{sigla}', '{uf}', '{fonte}', '{dateDiario}', 6);");

                        UpdateConsoleDownloadDJEN_D_(idfonte, data);
                    }
                    else
                    {
                        string folderPathFonteD = $"T:\\IO\\djen\\{dataFormatadaStringPasta_}\\D+";
                        string folderPathFonte = $"T:\\IO\\djen\\{dataFormatadaStringPasta_}";

                        List<LinhaArquivo> listaLinhasFonte = CarregarArquivoFonte(arquivoFonte, folderPathFonte, Encoding.GetEncoding("iso-8859-1"));

                        List<LinhaArquivo> listaLinhasFonteD = CarregarArquivoFonteD(arquivoFonte, folderPathFonteD, Encoding.GetEncoding("iso-8859-1"));

                        List<Publicacao> listPubs = ListarPublicacoesFonte(listaLinhasFonte);
                        List<Publicacao> listPubsD = ListarPublicacoesD(listaLinhasFonteD);

                        Console.WriteLine($"Realizando Comparação das fontes... {sigla}");

                        novasPublicacoes.Clear();

                        var encontradoPartes = false;
                        var encontradoAdvogados = false;
                        var encontradoTexto = false;
                        var encontradoNumeroProcesso = false;

                        var lNEncontrados = new List<string>();

                        foreach (var pubD in listPubsD.OrderBy(p => p.NumeroProcesso))
                        {
                            var publicacaoDtexto = pubD.TextoPublicacao;
                            var publicacaoDnumeroProcesso = pubD.NumeroProcesso;
                            var publicacaoDparteProcesso = pubD.PartesPublicacao;
                            var publicacaoDadvogadoProcesso = pubD.AdvogadosPublicacao;
                            var publicacaoDCompleta = pubD.PublicacaoCompleta;

                            var txt1 = RegExHandler.InvalidosRelacionamentoAntigo(publicacaoDtexto);

                            encontradoTexto = false;
                            encontradoNumeroProcesso = false;

                            foreach (var pub in listPubs.Where(i => i.NumeroProcesso == publicacaoDnumeroProcesso))
                            {
                                encontradoNumeroProcesso = true;

                                var publicacaoTexto = pub.TextoPublicacao;
                                var publicacaoNumeroProcesso = pub.NumeroProcesso;
                                var publicacaoparteProcesso = pub.PartesPublicacao;
                                var publicacaoadvogadoProcesso = pub.AdvogadosPublicacao;

                                var txt2 = RegExHandler.InvalidosRelacionamentoAntigo(publicacaoTexto);

                                if (txt1 == txt2)
                                {
                                    encontradoTexto = true;

                                    var lPartes = publicacaoparteProcesso.Split(',');
                                    var lAdvogados = publicacaoadvogadoProcesso.Split(',');

                                    foreach (var parte in lPartes)
                                    {
                                        try
                                        {
                                            publicacaoDparteProcesso = publicacaoDparteProcesso.Replace(parte, string.Empty).Trim();
                                        }
                                        catch { }
                                    }

                                    if (publicacaoDparteProcesso.Replace(",", string.Empty).Trim() == string.Empty)
                                    {
                                        encontradoPartes = true;
                                    }

                                    foreach (var adv in lAdvogados)
                                    {
                                        try
                                        {
                                            publicacaoDadvogadoProcesso = publicacaoDadvogadoProcesso.Replace(adv, string.Empty).Trim();
                                        }
                                        catch { }
                                    }

                                    if (publicacaoDadvogadoProcesso.Replace(",", string.Empty).Trim() == string.Empty)
                                    {
                                        encontradoAdvogados = true;
                                    }

                                    if (encontradoAdvogados && encontradoPartes)
                                    {
                                        lNEncontrados.Remove(publicacaoDnumeroProcesso);
                                    }
                                    else
                                    {
                                        lNEncontrados.Add(publicacaoDnumeroProcesso);
                                    }

                                    break;
                                }
                            }

                            if (!encontradoTexto || !encontradoNumeroProcesso)
                            {
                                lNEncontrados.Add(publicacaoDnumeroProcesso);

                                novasPublicacoes.Add(new NovaPublicacao { IdFonte = int.Parse(idfonte), CNJ = publicacaoDnumeroProcesso, Publicacao = publicacaoDCompleta, DataDisponibilizacao = DateTime.Parse(data) });
                            }
                            else
                            {
                                if (!encontradoAdvogados || !encontradoPartes)
                                {
                                    novasPublicacoes.Add(new NovaPublicacao { IdFonte = int.Parse(idfonte), CNJ = publicacaoDnumeroProcesso, Publicacao = publicacaoDCompleta, DataDisponibilizacao = DateTime.Parse(data) });
                                }
                            }
                        }

                        if (lNEncontrados.Count == 0)
                        {
                            DateTime dateformat = DateTime.ParseExact(data, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                            string dateDiario = dateformat.ToString("yyyy-MM-dd");

                            var IdDiario = RetornarIdDiario(idfonte, dateDiario);

                            Update_Diario_Status(IdDiario, 9);

                            UpdateConsoleDownloadDJEN_D(idfonte, data);
                        }
                        else
                        {
                            if (novasPublicacoes.Count > 0)
                            {
                                CriarArquivoSuplemento(novasPublicacoes, int.Parse(idfonte), DateTime.Parse(data));

                                DateTime dateformat = DateTime.ParseExact(data, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                                string dateDiario = dateformat.ToString("yyyy-MM-dd");

                                ExecuteCommand($"insert into [TLEGAL0].[dbo].[ConsoleDownloadDJEN] ([idfonte], [iduf], [sigla], [uf], [fonte], [data], [status]) values ({idfonte}, {iduf}, '{sigla}', '{uf}', '{fonte}', '{dateDiario}', 6);");

                                var IdDiario = RetornarIdDiario(idfonte, dateDiario);

                                Update_Diario_Status(IdDiario, 9);

                                UpdateConsoleDownloadDJEN_D_(idfonte, data);
                            }
                        }
                    }

                    Console.WriteLine("");
                    Console.WriteLine("");
                }
            }

            Console.WriteLine("CONSOLE COMPARAÇÃO DJEN FINALIZADO COM SUCESSO!");
            Console.WriteLine("TECLE CTRL + C PARA FECHAR");
            Console.ReadLine();
        }
        public static void ExecuteCommand(string sql)
        {
            SqlConnection myConnection = new SqlConnection("Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400");
            SqlCommand myCommand = new SqlCommand(sql, myConnection);
            myCommand.CommandType = CommandType.Text;

            try
            {
                myConnection.Open();
                myCommand.CommandTimeout = 0;
                myCommand.ExecuteNonQuery();
                myConnection.Close();
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
            finally
            {

                if (myConnection.State == ConnectionState.Open)
                    myConnection.Close();
            }

            myConnection.Dispose();
        }

        public struct NovaPublicacao
        {
            public int IdFonte { get; set; }
            public string CNJ { get; set; }
            public string Publicacao { get; set; }
            public DateTime DataDisponibilizacao { get; set; }
        }

        public static void CriarArquivoSuplemento(List<NovaPublicacao> novasPublicacoes, int idFonte, DateTime DataDisponibilizacao)
        {
            var pastaIO = @"\\corporate05\d$\IO\djen";
            var fonteIO = string.Empty;
            var fonteTitulo = string.Empty;
            FonteRepository fonteRepository = new FonteRepository();

            if (idFonte > 0)
            {
                var _fonte = fonteRepository.Retrieve(idFonte);
                fonteIO = _fonte.IO;
                fonteTitulo = _fonte.Titulo;
            }

            StringBuilder sBuilder = new StringBuilder();
            Encoding unicode = Encoding.Unicode;
            Encoding utf8 = Encoding.UTF8;
            Encoding iso = Encoding.GetEncoding("ISO-8859-1");

            sBuilder.AppendLine($"@TP@{fonteTitulo}");

            sBuilder.AppendLine("!**!1!**!");
            sBuilder.AppendLine("");

            var publicacoesBuild = new List<string>();

            foreach (var novaPublicacao in novasPublicacoes)
            {
                var publicacao_ = novaPublicacao.Publicacao;

                publicacao_ = HttpUtility.HtmlDecode(publicacao_);
                publicacao_ = HttpUtility.HtmlDecode(publicacao_);
                publicacao_ = Regex.Replace(publicacao_, @"\t|\n|\r", " ");
                publicacao_ = Regex.Replace(publicacao_.Replace("&nbsp;", " ").Replace("&NBSP;", " "), @"\s{2,}", " ");
                publicacao_ = Regex.Replace(publicacao_, @"(\b)\|(\b)", "$1 | $2");

                if (publicacao_.Contains("&AMP;"))
                {
                    publicacao_ = publicacao_.Replace("&AMP;", "&");
                }

                if (publicacao_.Contains("&NBSP;"))
                {
                    publicacao_ = publicacao_.Replace("&NBSP;", " ");
                }

                if (publicacao_.Contains("”") || publicacao_.Contains("“"))
                {
                    publicacao_ = publicacao_.Replace("”", "\"");
                    publicacao_ = publicacao_.Replace("“", "\"");
                }

                if (publicacao_.Contains("–") || publicacao_.Contains("-"))
                {
                    publicacao_ = publicacao_.Replace("–", "-");
                    publicacao_ = publicacao_.Replace("-", "-");
                }

                if (publicacao_.Contains("’"))
                {
                    publicacao_ = publicacao_.Replace("’", "'");
                }

                publicacao_ = publicacao_.Replace("Conteúdo meramente informativo,", " Conteúdo meramente informativo,");
                publicacao_ = publicacao_.Replace(" ", " ");
               
                if (publicacao_.Contains("Conteúdo meramente informativo, conforme ATO CONJUNTO TST.CSJT.GP Nº 77, de 27/10/2023. Consulte no Diário Eletrônico da Justiça do Trabalho a publicação oficial"))
                {
                    sBuilder.AppendLine($"&T2&CONTEÚDO MERAMENTE INFORMATIVO, CONFORME ATO CONJUNTO TST.CSJT.GP Nº 77, DE 27/10/2023. CONSULTE NO DIÁRIO ELETRÔNICO DA JUSTIÇA DO TRABALHO A PUBLICAÇÃO OFICIAL.");
                }

                sBuilder.AppendLine(publicacao_);

                publicacoesBuild.Add(publicacao_);
            }

            sBuilder.AppendLine("");
            
            if (!Directory.Exists($"{pastaIO}\\{DataDisponibilizacao.ToString("yyyyMMdd")}"))
            {
                Directory.CreateDirectory($"{pastaIO}\\{DataDisponibilizacao.ToString("yyyyMMdd")}");
            }

            if (!Directory.Exists($"{pastaIO}\\{DataDisponibilizacao.ToString("yyyyMMdd")}\\Suplemento"))
            {
                Directory.CreateDirectory($"{pastaIO}\\{DataDisponibilizacao.ToString("yyyyMMdd")}\\Suplemento");
            }

            var newFileName = $"{pastaIO}\\{DataDisponibilizacao.ToString("yyyyMMdd")}\\Suplemento\\{fonteIO}_{DataDisponibilizacao.ToString("ddMMyyyy")}.txt";

            var countSuplemento = 0;

            checkFileName:

            if (File.Exists(newFileName))
            {
                newFileName = $"{pastaIO}\\{DataDisponibilizacao.ToString("yyyyMMdd")}\\Suplemento\\{fonteIO}_{DataDisponibilizacao.ToString("ddMMyyyy")}_suplemento{countSuplemento}.txt";
                countSuplemento++;
            }
            else
            {
                goto resumeFileCreator;
            }

            goto checkFileName;

            resumeFileCreator:

            StreamWriter sfiletratado = new StreamWriter(newFileName, false, Encoding.GetEncoding("ISO-8859-1"));

            var stext = sBuilder.ToString();

            sfiletratado.Write(stext);
            sfiletratado.Close();
            sfiletratado.Dispose();

            return;
        }

        private static string TLEGALSERVICE = "";
        public static int FontesDisponiveis()
        {
            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";
            int qtdFontesDJEN = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string insertQuery = "SELECT COUNT(*) FROM [TLEGAL0].[dbo].[ConsoleDownloadDJEN_D+] WHERE STATUS = 1 AND publicacoescarregadas <> 0;";
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 0;
                    connection.Open();
                    qtdFontesDJEN = (int)command.ExecuteScalar();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                Log l = new Log();
                l.IdUsuario = 0;
                l.Tarefa = "Console ComparacaoDJEN erro consulta.";
                l.DtLog = DateTime.Now;
                l.Tipo = 8989;
                l.MensagemErro = $"Console ComparacaoDJEN: {ex.Message}";

                LogRepository.Save_Log(l, TLEGALSERVICE);
            }

            return qtdFontesDJEN;
        }
        public static List<Fontes> CarregarFontes()
        {
            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";

            List<Fontes> listFontes = new List<Fontes>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string insertQuery = "SELECT e.[idfonte], e.[iduf], e.[sigla], e.[uf], e.[fonte], e.[data], e.[publicacoescarregadas], e.[arquivocarregadas], e.[status], f.[IO] FROM [TLEGAL0].[dbo].[ConsoleDownloadDJEN_D+] e INNER JOIN [TLEGAL0].[dbo].[Fonte] f ON f.id = e.idfonte WHERE e.[status] = 1 AND e.publicacoescarregadas <> 0;";
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 0;

                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        Fontes fonte = new Fontes();
                        fonte.IdFonte = reader["idfonte"].ToString();
                        fonte.IdUf = reader["iduf"].ToString();
                        fonte.Sigla = reader["sigla"].ToString();
                        fonte.Uf = reader["uf"].ToString();
                        fonte.Fonte = reader["fonte"].ToString();
                        fonte.Data = reader["data"].ToString();
                        fonte.PublicacoesCarregadas = reader["publicacoescarregadas"].ToString();
                        fonte.ArquivoCarregadas = reader["arquivocarregadas"].ToString();
                        fonte.Status = reader["status"].ToString();
                        fonte.IO = reader["IO"].ToString();

                        listFontes.Add(fonte);
                    }

                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                Log l = new Log();
                l.IdUsuario = 0;
                l.Tarefa = "Console ComparacaoDJEN erro Leitura Base.";
                l.DtLog = DateTime.Now;
                l.Tipo = 8989;
                l.MensagemErro = $"Console ComparacaoDJEN: {ex.Message}";

                LogRepository.Save_Log(l, TLEGALSERVICE);
            }

            return listFontes;
        }
        public static string SearchPath(string data, string iduf, string sigla, string uf, string idfonte)
        {
            DateTime dataFormatada1 = DateTime.ParseExact(data, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            string dataFormatadaStringArquivo = dataFormatada1.ToString("ddMMyyyy");

            DateTime dateformat = DateTime.ParseExact(data, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            string dateDiario = dateformat.ToString("yyyy-MM-dd");

            var IdDiario = RetornarIdDiario(idfonte, dateDiario);

            if (IdDiario == 0)
            {
                return string.Empty;
            }

            var IO = RetornarIO(IdDiario);

            int indiceBarra = IO.IndexOf('\\');

            string novaString = IO.Substring(0, indiceBarra);

            var caminho = $"{novaString}_{dataFormatadaStringArquivo}";

            return caminho;
        }
        public static string GetTextBetweenPipes(string text)
        {
            int startPipe = text.IndexOf("|") + 1;
            int endPipe = text.IndexOf("|", startPipe);
            if (startPipe < endPipe)
            {
                return text.Substring(startPipe, endPipe - startPipe).Trim();
            }
            return "";
        }
        public static string GetLastChars(string text)
        {
            if (text.Length >= 10)
            {
                return text.Substring(text.Length - 200);
            }
            return text;
        }
        public static List<LinhaArquivo> CarregarArquivoFonte(string arquivoFonte, string folderPathFonte, Encoding encoding)
        {
            List<LinhaArquivo> listaLinhasFonte = new List<LinhaArquivo>();

            try
            {
                if (Directory.Exists(folderPathFonte))
                {
                    string arquivoEspecifico = Path.Combine(folderPathFonte, $"{arquivoFonte}.txt");

                    if (File.Exists(arquivoEspecifico))
                    {
                        Console.WriteLine($"Realizando a leitura das linhas da fonte: {arquivoEspecifico}");

                        using (StreamReader reader = new StreamReader(arquivoEspecifico, encoding))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                listaLinhasFonte.Add(new LinhaArquivo
                                {
                                    Fonte = arquivoEspecifico,
                                    Conteudo = line
                                });
                            }
                        }

                        Console.WriteLine("Linhas carregadas com sucesso.");

                        return listaLinhasFonte;
                    }
                    else
                    {
                        return listaLinhasFonte;
                    }
                }
                else
                {
                    Console.WriteLine($"O diretório {folderPathFonte} não foi encontrado.");
                    return listaLinhasFonte;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ler arquivos: {ex.Message}");
                return listaLinhasFonte;
            }
        }
        public static List<LinhaArquivo> CarregarArquivoFonteD(string arquivoFonte, string folderPathFonte, Encoding encoding)
        {
            List<LinhaArquivo> listaLinhasFonteD = new List<LinhaArquivo>();

            if (Directory.Exists(folderPathFonte))
            {
                string arquivoEspecifico = Path.Combine(folderPathFonte, $"{arquivoFonte}.txt");

                if (File.Exists(arquivoEspecifico))
                {
                    Console.WriteLine($"Realizando agora a leitura das linhas da fonte D+: {arquivoEspecifico}");


                    using (StreamReader reader = new StreamReader(arquivoEspecifico, encoding))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            listaLinhasFonteD.Add(new LinhaArquivo
                            {
                                Fonte = arquivoEspecifico,
                                Conteudo = line
                            });
                        }
                    }

                    Console.WriteLine($"Linhas carregadas com sucesso");
                    return listaLinhasFonteD;
                }
                else
                {
                    return listaLinhasFonteD;
                }
            }
            else
            {
                Console.WriteLine($"O diretório {folderPathFonte} não foi encontrado.");
                return listaLinhasFonteD;
            }
        }
        public static List<Publicacao> ListarPublicacoesFonte(List<LinhaArquivo> listaLinhasFonte)
        {
            List<Publicacao> listPubs = new List<Publicacao>();

            foreach (var pubs in listaLinhasFonte)
            {
                var linhaPub = pubs.Conteudo;

                if (linhaPub != null && linhaPub.StartsWith("#NP#Processo:"))
                {
                    string publicacao = $"{linhaPub.Split('|')[0].Trim()}|{linhaPub.Split('|').Last().Trim().Replace(" ", "")}";

                    string processoNumero = linhaPub.Split('|')[0].Replace("#NP#Processo:", string.Empty).Trim();

                    string partes = string.Empty;

                    string advogados = string.Empty;

                    foreach (var p in linhaPub.Split('|'))
                    {
                        if (p.Trim().StartsWith("Parte(s):"))
                        {
                            partes = p.Trim().Replace("Parte(s):", string.Empty).Trim();
                        }

                        if (p.Trim().StartsWith("Advogado(s):"))
                        {
                            advogados = p.Trim().Replace("Advogado(s):", string.Empty).Trim();
                        }
                    }

                    listPubs.Add(new Publicacao
                    {
                        NumeroProcesso = processoNumero,
                        TextoPublicacao = publicacao,
                        PartesPublicacao = partes,
                        AdvogadosPublicacao = advogados,

                    });
                }
            }

            return listPubs;
        }
        public static List<Publicacao> ListarPublicacoesD(List<LinhaArquivo> listaLinhasFonteD)
        {
            List<Publicacao> listPubsD = new List<Publicacao>();
            foreach (var pubs in listaLinhasFonteD)
            {
                string linhaPub = pubs.Conteudo;

                if (linhaPub != null && linhaPub.StartsWith("#NP#Processo:"))
                {
                    string publicacao = $"{linhaPub.Split('|')[0].Trim()}|{linhaPub.Split('|').Last().Trim().Replace(" ", "")}";

                    string processoNumero = linhaPub.Split('|')[0].Replace("#NP#Processo:", string.Empty).Trim();

                    string partes = string.Empty;

                    string advogados = string.Empty;

                    string publicacaoCompleta = linhaPub;

                    foreach (var p in linhaPub.Split('|'))
                    {
                        if (p.Trim().StartsWith("Parte(s):"))
                        {
                            partes = p.Trim().Replace("Parte(s):", string.Empty).Trim();
                        }

                        if (p.Trim().StartsWith("Advogado(s):"))
                        {
                            advogados = p.Trim().Replace("Advogado(s):", string.Empty).Trim();
                        }
                    }

                    listPubsD.Add(new Publicacao
                    {
                        NumeroProcesso = processoNumero,
                        TextoPublicacao = publicacao,
                        PartesPublicacao = partes,
                        AdvogadosPublicacao = advogados,
                        PublicacaoCompleta = publicacaoCompleta

                    });
                }
            }

            return listPubsD;
        }

        public static int RetornarIdDiario(string idfonte, string data)
        {
            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";
            int idDiario = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string Query = $"SELECT [Id] FROM [TLEGAL0].[dbo].[Diario] where idfonte = {idfonte} and DtCirc = '{data}' and Suplemento = 0;"; //and status = -8 
                    SqlCommand command = new SqlCommand(Query, connection);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 0;
                    connection.Open();
                    idDiario = (int)command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Log l = new Log();
                l.IdUsuario = 0;
                l.Tarefa = "Console ComparacaoDJEN erro consulta.";
                l.DtLog = DateTime.Now;
                l.Tipo = 8989;
                l.MensagemErro = $"Console ComparacaoDJEN: {ex.Message}";

                LogRepository.Save_Log(l, TLEGALSERVICE);
            }

            return idDiario;
        }
        public static void Update_Diario_Status(int id, int status)
        {
            SqlConnection myConnection = new SqlConnection(ConexaoHandler.TLEGAL0);
            SqlCommand myCommand = new SqlCommand("Update_Diario_Status", myConnection);
            myCommand.CommandType = CommandType.StoredProcedure;
            myCommand.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            myCommand.Parameters.Add("@Status", SqlDbType.Int).Value = status;

            try
            {
                myConnection.Open();
                myCommand.CommandTimeout = 0;
                myCommand.ExecuteNonQuery();
                myConnection.Close();
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
            finally
            {

                if (myConnection.State == ConnectionState.Open)
                    myConnection.Close();
            }

            myConnection.Dispose();
        }
        public static void UpdateConsoleDownloadDJEN_D_(string idfonte, string data)
        {
            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string insertQuery = $"UPDATE [TLEGAL0].[dbo].[ConsoleDownloadDJEN_D+] SET [status] = 3 where idfonte = {idfonte} and data = '{DateTime.Parse(data).ToString("yyyy-MM-dd")}';";
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    connection.Open();
                    command.CommandTimeout = 0;
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
        }
        public static void UpdateConsoleDownloadDJEN_D(string idfonte, string data)
        {
            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string insertQuery = $"UPDATE [TLEGAL0].[dbo].[ConsoleDownloadDJEN_D+] SET [status] = 2 where idfonte = {idfonte} and data = '{DateTime.Parse(data).ToString("yyyy-MM-dd")}';";
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    connection.Open();
                    command.CommandTimeout = 0;
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
        }
        public static string RetornarIO(int IdDiario)
        {
            TLEGALSERVICE = ConexaoHandler.DATABASECHAVEADOR.Replace("database=TLEGAL2;", "database=TLEGAL02;").Replace("database=TLEGAL1;", "database=TLEGAL01;").Replace("database=TLEGAL0", "database=TLEGAL0");
            string connectionString = "Server=corporate03;Database=TLEGAL0;Uid=textractor;Pwd=okmnji90;pooling=true;connection lifetime=0;min pool size = 1;max pool size=400";
            string IO = string.Empty;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string Query = $"select [IO] from [TLEGAL0].[dbo].[Diario] where id = {IdDiario}";
                    SqlCommand command = new SqlCommand(Query, connection);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 0;
                    connection.Open();
                    IO = (string)command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Log l = new Log();
                l.IdUsuario = 0;
                l.Tarefa = "Console ComparacaoDJEN erro consulta.";
                l.DtLog = DateTime.Now;
                l.Tipo = 8989;
                l.MensagemErro = $"Console ComparacaoDJEN: {ex.Message}";

                LogRepository.Save_Log(l, TLEGALSERVICE);
            }

            return IO;
        }

    }
    public class LinhaArquivo
    {
        public string Fonte { get; set; }
        public string Conteudo { get; set; }
    }
    public class Fontes
    {
        public string IdFonte { get; set; }

        public string IdUf { get; set; }

        public string Sigla { get; set; }

        public string Uf { get; set; }

        public string Fonte { get; set; }

        public string Data { get; set; }

        public string PublicacoesCarregadas { get; set; }

        public string ArquivoCarregadas { get; set; }

        public string Status { get; set; }

        public string IO { get; set; }

    }
    public class Publicacao
    {
        public string NumeroProcesso { get; set; }
        public string TextoPublicacao { get; set; }
        public string PartesPublicacao { get; set; }
        public string AdvogadosPublicacao { get; set; }
        public string PublicacaoCompleta { get; set; }

    }

}
