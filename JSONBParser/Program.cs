using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

public class Program
{
  static void Main()
  {

    // Test case 1: "1"
    byte[] data = Convert.FromHexString("1331");  
    JSONB parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 2: "1"
    data = Convert.FromHexString("c30131");  
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 3: "1"
    data = Convert.FromHexString("d3000131"); 
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 4: "1"
    data = Convert.FromHexString("e30000000131"); 
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 5: "1"
    data = Convert.FromHexString("f3000000000000000131"); 
    parser = new JSONB(data);
    Console.WriteLine(parser);

    /*
      See "test_data/test.sql" for more blob examples. 
      All of the hexstrings were converted from "jsonb()" functions
    */

    // Test case 6: JSON "null"
    data = Convert.FromHexString("00");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 7: JSON "true"
    data = Convert.FromHexString("01");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 8: JSON "false"
    data = Convert.FromHexString("02");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 9: JSON INT (1234)
    data = Convert.FromHexString("4331323334");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 10: JSON Float (3.14)
    data = Convert.FromHexString("45332e3134");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 11: JSON Text ("hi my name is steven pak!")
    data = Convert.FromHexString("c7196869206d79206e616d652069732073746576656e2070616b21");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 12: JSON Text ("")
    data = Convert.FromHexString("07");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 13: JSON Text ("💩")
    data = Convert.FromHexString("47f09f92a9");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 14: JSON Array ([1,2,3,4,"5",6,7,8,9,10])
    data = Convert.FromHexString("cb15133113321333133417351336133713381339233130");
    parser = new JSONB(data);
    Console.WriteLine(parser);

    // Test case 15: JSON Object ({"user":{"first":"steven","last":"pak"}})
    data = Convert.FromHexString("cc1d4775736572cc165766697273746773746576656e476c6173743770616b");
    parser = new JSONB(data);
    Console.WriteLine(parser);
  }
}

/// <summary>
/// A basic class that provides a simple API into parsing a "JSONB" blob according to 
/// the encoding described here: https://sqlite.org/draft/jsonb.html 
/// </summary>
public class JSONB
{

  public JSONB(byte[] data)
  {
    using (MemoryStream ms = new MemoryStream(data))
    {
      ParseJSONB(ms);
    }
  }

  public JSONB(Stream stream)
  {
    ParseJSONB(stream);
  }

  public uint HeaderSize { get; private set; }

  public ulong PayloadSize { get; private set; } 

  public JSONElementType ElementType { get; private set; }

  public object? InterpretedPayloadData { get; private set; }

  /// <summary>
  /// Parses the header of a JSONB blob, populating various instance properties
  /// </summary>
  /// <param name="data"></param>
  /// <returns></returns>
  public string ParseJSONB(Stream stream)
  {
    // "HeaderSize" - upper 4-bits
    //
    // case 1: 0 <= x <= 11, then HeaderSize = 1 AND x == "PayloadSize"
    byte headerByte = (byte) stream.ReadByte();
    byte possiblePaySize = (byte)((headerByte & 0xF0) >> 4);
    if (possiblePaySize <= 11)
    {
      HeaderSize = 1;
      PayloadSize = possiblePaySize;
    }
    // case 2: 12 <= x <= 15, then HeaderSize is found further in the stream, based on "x"
    else
    {
      byte[] payloadSizeBuffer = new byte[8];
      if (possiblePaySize == 12) 
      {
        HeaderSize = 2;
        PayloadSize = (ulong) stream.ReadByte();
      }
      else if (possiblePaySize == 13)
      {
        HeaderSize = 3;
        stream.Read(payloadSizeBuffer, 0, 2);
        PayloadSize = BinaryPrimitives.ReadUInt16BigEndian(payloadSizeBuffer);
      }
      else if (possiblePaySize == 14)
      {
        HeaderSize = 5;
        stream.Read(payloadSizeBuffer, 0, 4);
        PayloadSize = BinaryPrimitives.ReadUInt32BigEndian(payloadSizeBuffer);
      }
      else 
      {
        HeaderSize = 9;
        stream.Read(payloadSizeBuffer, 0, 8);
        PayloadSize = BinaryPrimitives.ReadUInt64BigEndian(payloadSizeBuffer);
      }
    }

    // Element Type
    byte elementType = (byte) (headerByte & 0x0F);
    ElementType = (JSONElementType) elementType;

    // InterpretedPayloadData
    InterpretedPayloadData = InterpretRawPayload(stream, ElementType);

    return JsonSerializer.Serialize(InterpretedPayloadData);
  }

  /// <summary>
  /// Takes in a "stream" positioned where the "elementType" payload is, reads it,
  /// and returns its respective JSON-to-C# object type based on "elementType"
  /// e.g.
  /// INT -> int
  /// TEXT -> string
  /// NULL -> null
  /// FLOAT -> float
  /// </summary>
  /// <param name="stream"></param>
  /// <param name="elementType"></param>
  /// <returns></returns>
  public object? InterpretRawPayload(Stream stream, JSONElementType elementType)
  {
    // RawPayloadData - read stream in each specific case
    var RawPayloadData = new byte[PayloadSize];

    // encodings
    ASCIIEncoding ascii = new ASCIIEncoding();
    UTF8Encoding utf8 = new UTF8Encoding();

    switch (elementType)
    {
      case JSONElementType.NULL:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return null;

      case JSONElementType.TRUE:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return true;

      case JSONElementType.FALSE:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return false;

      case JSONElementType.INT:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        var stringifiedInt = ascii.GetString(RawPayloadData);   // JSON only support 2^53
        return Convert.ToInt32(stringifiedInt);
        
      case JSONElementType.INT5:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return "TODO";

      case JSONElementType.FLOAT:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        var stringifiedFloat = ascii.GetString(RawPayloadData);
        return Convert.ToDouble(stringifiedFloat);

      case JSONElementType.FLOAT5:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return "TODO";

      case JSONElementType.TEXT:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return utf8.GetString(RawPayloadData);

      case JSONElementType.TEXTJ:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return "TODO";

      case JSONElementType.TEXT5:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return "TODO";

      case JSONElementType.TEXTRAW:
        stream.Read(RawPayloadData, 0, (int) PayloadSize);
        return "TODO";

      case JSONElementType.ARRAY:
        uint bytesRead = 0;
        List<object?> list = new List<object?>();
        while (bytesRead < PayloadSize)
        {
          var jsonb = new JSONB(stream);
          list.Add(jsonb.InterpretedPayloadData);
          bytesRead += jsonb.HeaderSize + (uint) jsonb.PayloadSize;
        }
        return list;
      
      case JSONElementType.OBJECT:
        bytesRead = 0;
        Dictionary<string, object?> dict = new Dictionary<string, object?>();
        while (bytesRead < PayloadSize)
        {
          var keyJsonb = new JSONB(stream);
          string key = (string) keyJsonb.InterpretedPayloadData!;
          var valueJsonb = new JSONB(stream);
          var value = valueJsonb.InterpretedPayloadData;
          dict[key] = value;
          bytesRead += keyJsonb.HeaderSize + (uint) keyJsonb.PayloadSize + valueJsonb.HeaderSize + (uint) valueJsonb.PayloadSize;
        }
        return dict;
    }
    return "";
  }

  /// <summary>
  /// Returns JSONB Properties
  /// </summary>
  /// <returns></returns>
  public override string ToString()
  {
      return $"HeaderSize: {HeaderSize}\nPayloadSize: {PayloadSize}\nJSON Element Type: {ElementType}\nInterpreted Data For {ElementType}: {JsonSerializer.Serialize(InterpretedPayloadData)}\n";
  }

  public enum JSONElementType: byte
  {
    /// <summary>
    /// The element is a JSON "null". The payload size for a true JSON NULL must must be zero. 
    /// Future versions of SQLite might extend the JSONB format with elements that have a zero element type 
    /// but a non-zero size. In that way, legacy versions of SQLite will interpret the element as a NULL for backwards compatibility 
    /// while newer versions will interpret the element in some other way.
    /// </summary>
    NULL,
    TRUE,
    FALSE,
    INT,
    INT5,
    FLOAT,
    FLOAT5,
    TEXT,
    TEXTJ,
    TEXT5,
    TEXTRAW,
    ARRAY,
    OBJECT,
    RESERVED13,
    RESERVED14,
    RESERVED15
  }
}
