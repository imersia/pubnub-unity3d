// ******************************************************************************************************************************************************
// Copyright Imersia Ltd, February 2013
// Version 130407-01
//
// For information on the Pubnub REST API, go here: http://www.pubnub.com/tutorial/http-rest-push-api
// ******************************************************************************************************************************************************

using System;
using System.Collections;
using JSONEncoderDecoder;
using System.Xml;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using UnityEngine;

public delegate void stringCallback(string message);				// The callback function


class MyWebClient : WebClient {										// Increase the number of simultaneous connections allowed
    protected override WebRequest GetWebRequest(Uri address) {		// to one web resource
    	HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
    	req.ServicePoint.ConnectionLimit = 10;
    	return (WebRequest)req;
    }
}


public class PubnubThreads
{
    private string PubnubURL = "http://pubsub.pubnub.com/";			// URL for the Pubnub REST API
    static private Hashtable threadPool = new Hashtable();			// A pool of threads, one for each channel

    private string pubnubSubKey = "";
    private string pubnubPubKey = "";
	private string pubnubSecretKey = "";
	
	public bool stopped = false;

    // ****************************************************************************************************
    // Constructors
    // ****************************************************************************************************
    public PubnubThreads(string subscribeKey, string publishKey)
	{
        pubnubSubKey = subscribeKey;
        pubnubPubKey = publishKey;
	}
	public PubnubThreads(string subscribeKey, string publishKey, string secretKey)
    {
        pubnubSubKey = subscribeKey;
        pubnubPubKey = publishKey;
		pubnubSecretKey = secretKey;
		
    }
    // ****************************************************************************************************



    // ****************************************************************************************************
    // Destructor
    // ****************************************************************************************************
    ~PubnubThreads()
    {
        KillAll();
    }
    // ****************************************************************************************************



    // ****************************************************************************************************
    // Determine if we have Internet access of not
    // ****************************************************************************************************
    public static bool HasConnection()
    {
        try
        {
            System.Net.IPHostEntry i = System.Net.Dns.GetHostEntry("www.google.com");
            return true;
        }
        catch
        {
            return false;
        }
    }
    // ****************************************************************************************************



    // ****************************************************************************************************
    // Return a text-based status of the given channel
    // ****************************************************************************************************
    public string Status(string channel)
    {
        if (HasConnection())
        {
            Thread oThread = (Thread)threadPool[channel];
			if (oThread != null)
			{
	            if (oThread.IsAlive)
	                return "OK";
	            else
	                return "Thread Error";
			}
			else
				return "";
        }
        else
        {
            return "Offline";
        }
    }
    // ****************************************************************************************************
   


    // ****************************************************************************************************
    // Publish a text, Array or Hashtable message to the Pubnub Channel
    // ****************************************************************************************************
    public void Publish(string channel, object theMessage, stringCallback successFunction, stringCallback failureFunction)
    {	
        Thread oThread = new Thread(new ThreadStart(() => { PublishThread(channel, theMessage, successFunction, failureFunction); }));
        oThread.Start();
    }
	public void PublishThread(string channel, object theMessage, stringCallback successFunction, stringCallback failureFunction)
    {
        string output = "";
		
        try
        {
            string queryString = "publish/" + pubnubPubKey + "/" + pubnubSubKey + "/0/" + channel + "/0/";
            if (theMessage is string)
            {
                queryString += "\"" + (string)theMessage + "\"";
            }
            else if ((theMessage is ArrayList) || (theMessage is Hashtable))
            {
                queryString += JSON.JsonEncode(theMessage);
            }
            else // Probably a number
            {
                queryString += theMessage.ToString();
            }
						
			WebClient client = new MyWebClient ();
        	client.Headers.Add ("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
			Stream data = client.OpenRead (PubnubURL + queryString);
        	StreamReader reader = new StreamReader (data);
			output = reader.ReadToEnd ();
			data.Close ();
        	reader.Close ();
			
			successFunction(output);
        }
        catch (Exception e)
        {
            output = "error : " + e.Message + " " + e.Source + " " + e.StackTrace;
			failureFunction(output);
        }
    }
    // ****************************************************************************************************



    // ****************************************************************************************************
    // Subscribe to a Pubnub Channel
    // ****************************************************************************************************
    public void Subscribe(string channel, stringCallback theCallback)
    {
        if (!threadPool.ContainsKey(channel))
        {
            Thread oThread = new Thread(new ThreadStart(() => { SubscribeThread(channel, theCallback); }));
			oThread.IsBackground = true;
            threadPool.Add(channel, oThread);
            oThread.Start();
        }
        else
        {
            Thread oThread = (Thread)threadPool[channel];
            if (!oThread.IsAlive)
            {
                threadPool.Remove(channel);
                oThread = new Thread(new ThreadStart(() => { SubscribeThread(channel, theCallback); }));
				oThread.IsBackground = true;
                threadPool.Add(channel, oThread);
                oThread.Start();
            }
        }
    }

    private void SubscribeThread(string channel, stringCallback theCallback)
    {
        string output = "";
        string queryString = "";
        string timeToken = "0";
		WebRequest objRequest = null;
		Debug.Log ("Thread " + channel + " started");

        while (!stopped && threadPool.ContainsKey (channel))
        {
            try
            {
                // Create the Query URL
                queryString = "subscribe/" + pubnubSubKey + "/" + channel + "/0/" + timeToken;
				
				WebClient client = new MyWebClient ();
	        	client.Headers.Add ("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
				Stream data = client.OpenRead (PubnubURL + queryString);
	        	StreamReader reader = new StreamReader (data);
				output = reader.ReadToEnd ();
				data.Close ();
	        	reader.Close ();

                // Convert it to a form we can work with, namely an Array with the first element an Array of messages
                // And the second element the timeToken
                ArrayList outputArray = (ArrayList)JSON.JsonDecode(output);
                if (outputArray != null)
                {
                    // The timeToken, used to make sure we get new messages next time around
                    timeToken = (string)outputArray[1];

                    // The messages
                    ArrayList messageArray = (ArrayList)outputArray[0];

                    // Call the Callback function for each message, turning it into text on the way if necessary
                    foreach (object message in messageArray)
                    {
                        if (message is string)
                        {
                            theCallback((string)message);
                        }
                        else if ((message is Hashtable) || (message is ArrayList))
                        {
                            theCallback(JSON.JsonEncode(message));
                        }
                        else // Probably a number
                        {
                            theCallback(message.ToString());
                        }
                    }
                }
            }
			catch (WebException w)
			{
				if (objRequest != null) objRequest.Abort ();
			}
			catch (ThreadAbortException a)
			{
				if (objRequest != null) objRequest.Abort ();
			}
			catch (ThreadInterruptedException i)
			{
				if (objRequest != null) objRequest.Abort ();
			}
            catch (Exception e)
            {
				if (objRequest != null) objRequest.Abort ();
			}

            // Give some other threads a chance
            Thread.Sleep(100);
        }
		
		if (objRequest != null) objRequest.Abort ();
		Debug.Log ("Thread " + channel + " stopped");
    }
    // ****************************************************************************************************



    // ****************************************************************************************************
    // Kill the thread so no more messages are received from this channel
    // ****************************************************************************************************	
    public void Unsubscribe(string channel)
    {
        if (threadPool.ContainsKey(channel))
        {
            Thread oThread = (Thread)threadPool[channel];
            threadPool.Remove(channel);
        }
    }
    // ****************************************************************************************************



    // ****************************************************************************************************
    // Kill all the Threads - call this when the Application Quits to make sure there are no odd threads
    // lying around.
    // ****************************************************************************************************
    public void KillAll()
    {
		stopped = true;
		
		bool allStopped = false;
		
		while (!allStopped)
		{
			allStopped = true;
			foreach (string theKey in threadPool.Keys)
	        {
	            Thread oThread = (Thread)threadPool[theKey];
				//oThread.Abort();
	            if (oThread.IsAlive) allStopped = false;
	        }
		}
		
        threadPool.Clear();
    }
    // ****************************************************************************************************
}
