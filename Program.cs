// See https://aka.ms/new-console-template for more information
using ctest;

Console.WriteLine("Hello, World!");



while (true)
{

    using (var x = new Ih110Connector())
        try
        {
            _ = Task.Run(() =>
            {

                x.Connect();

            }); 
            Console.WriteLine("ALINAN BUTONU");
            Console.ReadLine();

            Console.WriteLine("ALINAN OK");
            x.Read();

            //Console.ReadLine();


            var xxx = new MessageParser();

            var ss = xxx.Parse(x._serialNumbers,x._notSerialNumbers);
            Console.WriteLine(ss.ToString());
            x.DisposePort();

            // Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            x.DisposePort();
        }


}