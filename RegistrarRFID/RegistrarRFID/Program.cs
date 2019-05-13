using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Configuration;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System.IO.Ports;

namespace RegistrarRFID
{
    public class Program
    {
        //El EndpointUrl y Primary Key sirve para conectarse a la cuenta creada de Azure CosmosDB, esas llaves son copiadas del portal azure
        private static readonly string EndpointUri = ConfigurationManager.AppSettings["EndPointUrl"];
        private static readonly string PrimaryKey = ConfigurationManager.AppSettings["PrimaryKey"];
        private DocumentClient client;

        //Programa main
        static void Main(string[] args)
        {
            while (true)
            {
                //Se crea la conexion con el puerto serial, se debe usar el nombre del puerto que usa arduino, en este caso COM4
                SerialPort myport = new SerialPort();
                myport.BaudRate = 9600;
                myport.PortName = "com4";
                //Abre el puerto para recibir informacion
                if (myport.IsOpen)
                {
                    myport.Close();
                    myport.Dispose();
                }

                myport.Open();
                //Manejador de eventos (leer datos)
                myport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                Console.ReadKey();
            }
        }

        //Manejador de eventos
        private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs ex)
        {
            //Captura cualquier linea enviada a traves del puerto serial por Arduino y lo almacena en la variable indata
            SerialPort myPort = (SerialPort)sender;
            string indata = myPort.ReadLine();

            //Ejecuta el programa y captura cualquier error y lo muestra en pantalla
            try
            {
                Program p = new Program();
                //Ejecuta el procedimiento GetStartedDemo y le pasa como parametro el codigo RFID leido
                p.GetStartedDemo(indata).Wait();
            }
            catch (DocumentClientException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine($"{de.StatusCode} error occurred: {de.Message}, Message: {baseException.Message}");
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine($"Error, el rfid escaneado ya se encuentra registrado");
            }
            finally
            {
                Console.WriteLine("Procedimiento terminado, escanee otra tarjeta o presione una tecla para salir");
            }
        }

        //Instancia un nuevo DocumentClient para establecer la conexion con CosmosDB
        private async Task GetStartedDemo(string indata)
        {
            //crea un nuevo cliente y establece la conexion con el Azure CosmosDB
            this.client = new DocumentClient(new Uri(EndpointUri), PrimaryKey);
            //Crea una base de datos dentro de CosmosDB en caso esta no haya sido creada, llamda LibrosDB
            await this.client.CreateDatabaseIfNotExistsAsync(new Database { Id = "LibrosDB" });
            //Crea una Coleccion de documentos en la base de datos LibrosDB en caso no haya sido creada, la coleccion se llamara RfidCollection
            await this.client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("LibrosDB"), new DocumentCollection { Id = "RfidCollection" });

            //crea una entidad llamada rfid que almacenara el codigo Rfid escaneado y un estado
            Rfid documento = new Rfid
            {
                Id = indata,
                Estado = false
            };

            //Llamada al metodo de crear documento en caso no exista, pasando como paramero la BD. la coleccion y el objeto RFID
            await this.CreateRfidDocumentIfNotExists("LibrosDB", "RfidCollection", documento);
        }

        private void WriteToConsoleAndPromptToContinue(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        //estructura del documento que se guardara en la coleccion
        public class Rfid
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            public bool Estado { get; set; }
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        //tarea que crea el documento en caso no exista
        private async Task CreateRfidDocumentIfNotExists(string databaseName, string collectionName, Rfid rfid)
        {
            await this.client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), rfid);
            this.WriteToConsoleAndPromptToContinue("Se ha registrado correctamente el Rfid: {0}", rfid.Id);

        }
    }
}
