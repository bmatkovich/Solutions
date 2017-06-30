using System;
using System.Data;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.VisualBasic;

namespace _835Concat
{
public class TradingPartner
{
   public String Name { get; set; }
   public int ID { get; set; }

}
	class Program
	{
		public struct WriteOffItem
		{
			public string encounterNumber;
			public string lastName;
			public string firstName;
			public string middleName;
			public string claimDate;
			public string writeOffType;
			public string procCode;
			public string itemAmount;
			public string adjustmentCode;


			public WriteOffItem(string writeOffTypeArgument)
			{
				writeOffType = "Claim";
				if (string.Compare(writeOffTypeArgument,"Claim") != 0)
				{
					writeOffType = "LineItem";
				}
				lastName = string.Empty;
				firstName =string.Empty;
				middleName = string.Empty;
				procCode = string.Empty;
				itemAmount = string.Empty;
				middleName = string.Empty;
				claimDate = string.Empty;
				encounterNumber = string.Empty;
				adjustmentCode = string.Empty;
			}
		}
		static void Main(string[] args)
		{
			Process currentProcess = Process.GetCurrentProcess();
			string processName = currentProcess.ProcessName.Replace(".vshost", string.Empty);
			string baseFolder = Environment.GetEnvironmentVariable("BatchLocation");
			string outputFolder = baseFolder + @"ProcessData\" + processName + "\\";
			string sourceFolder = baseFolder + @"ProcessData\" + processName + @"\SourceFiles\";
			string fileName = processName + "~" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".x12";
			bool successfulCompletion = false;
			Exception exceptionCatcher = null;
			List<bool> initialISA = new List<bool>();
			initialISA.Add(true);
			List<bool> initialGS = new List<bool>();
			List<bool> initialST = new List<bool>();
			List<char> delimiter = new List<char>();
			char tempDelimiter = '~';
			List<char> fieldSeparator = new List<char>();
			char tempFieldSeparator = '*';
			List<string> saveIEA = new List<string>();
			List<string> saveGE = new List<string>();
			List<int> lxCounter = new List<int>();
			List<int> segmentCounter = new List<int>();
			List<int> plbCounter = new List<int>();
			string transactionDate = string.Empty;
			string checkNumber = string.Empty;
			int tradePartnerIndex = -1;
			List<bool> crlf = new List<bool>();	//  Some files use CRLF as segment delimiter
			bool tempCrLf = false;
			List<bool> rogueCrLf= new List<bool>();				//  Some files come with a segment delimiter as well as CRLF at end of each segment.  
			bool tempRogueCrLf = false;
			char[] charRead = new char[1];
			bool zeroFileFound = false;
			bool notAllFilesProcessed = true;
			int doneFilesCounter = 0;
			StreamReader writeOffs = new StreamReader(outputFolder + processName + "_WriteOffs_" + DateTime.Now.ToString("yyyyMMdd") + "csv");
			List<StreamWriter> outStreams = new List<StreamWriter>();
			List<StreamWriter> reportStreams = new List<StreamWriter>();
			List<StringBuilder> plbStringBuilder = new List<StringBuilder>();
			List <string> tradePartners = new List<string>();
			while (notAllFilesProcessed)
			{
				string[] fileEntries = Directory.GetFiles(sourceFolder);
				doneFilesCounter = 0;
				if (fileEntries.Length == 0)
				{
					notAllFilesProcessed = false;
					continue;
				}

				foreach (string targetFile in fileEntries)
				{
					if (targetFile.EndsWith(".Done"))
					{
						doneFilesCounter++;
						if (doneFilesCounter == fileEntries.Length)
						{
							notAllFilesProcessed = false;
							break;
						}
						continue;
					}
					int sourceFilesPosition = targetFile.IndexOf("SourceFiles");
					int beginCheckNumberPosition = targetFile.IndexOf("-", sourceFilesPosition+12);
					int endCheckNumberPosition = targetFile.IndexOf("-", beginCheckNumberPosition+1);
					checkNumber = targetFile.Substring(beginCheckNumberPosition + 1, endCheckNumberPosition - beginCheckNumberPosition - 1);
					int beginOfTransactionDate = targetFile.IndexOf("-", endCheckNumberPosition + 1);
					int endOfTransactionDate = targetFile.IndexOf("-", beginOfTransactionDate + 1);
					transactionDate = targetFile.Substring(beginOfTransactionDate + 1, endOfTransactionDate - beginOfTransactionDate - 1);
					tempCrLf = false;
					tempDelimiter = '~';
					tempFieldSeparator = '*';
					tempRogueCrLf = false;
					bool lxFound = false;
					WriteOffItem currentClaimWriteOffItem;
					WriteOffItem currentProcedureWriteOffItem;
					zeroFileFound = ZeroDollarFile(targetFile, ref tempDelimiter, ref tempFieldSeparator, ref tempCrLf, ref tempRogueCrLf);
					StreamReader sr = new StreamReader(targetFile);
					char[] buffer = ParseISA(ref tempDelimiter, ref tempFieldSeparator, ref tempCrLf, ref tempRogueCrLf, charRead, sr);

					if (zeroFileFound)
					{
						//+
						//  Determine trading partner and initialize output file for new trading partner instance in this run
						//-
						tradePartnerIndex = FindTradingPartner(buffer, ref tradePartners, transactionDate);
						if (initialISA[tradePartnerIndex])
						{
							InitializeNewTradingPartner(processName, outputFolder, ref initialISA, tempDelimiter, tempFieldSeparator, ref segmentCounter, ref plbCounter, tradePartnerIndex, tempCrLf, tempRogueCrLf, ref outStreams, ref plbStringBuilder, tradePartners, ref initialGS, ref initialST, ref delimiter, ref fieldSeparator, ref saveIEA, ref saveGE, ref lxCounter, ref crlf, ref rogueCrLf, ref reportStreams);
							outStreams[tradePartnerIndex].AutoFlush = true;
							reportStreams[tradePartnerIndex].AutoFlush = true;
							outStreams[tradePartnerIndex].Write(buffer);
							if (crlf[tradePartnerIndex])
							{
								outStreams[tradePartnerIndex].Write(delimiter);
							}
						}
					}
					char[] a = new char[1];

					while (sr.Peek() > -1)
					{
						StringBuilder line = ReadSegment(delimiter[tradePartnerIndex], sr);

						switch (line.ToString().Substring(0, 3))
						{
							case "ISA":
								{
									if (zeroFileFound)
									{
										if (initialISA[tradePartnerIndex] == false)
										{
											continue;
										}
									}

									break;
								}
							case "IEA":
								{
									if (zeroFileFound)
									{
										if (initialISA[tradePartnerIndex])
										{
											initialISA[tradePartnerIndex] = false;
											saveIEA[tradePartnerIndex] = line.ToString();
										}
									}
									continue;
								}
							case "ST*":
								{
									if (zeroFileFound)
									{
										if (initialST[tradePartnerIndex])
										{
											string tempString = line.ToString();
											string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
											if (crlf[tradePartnerIndex])
											{
												segments[2] = "000000001\r\n";
											}
											else
											{
												segments[2] = "000000001" + delimiter[tradePartnerIndex];
											}
											line.Length = 0;
											line.Append(String.Join(fieldSeparator[tradePartnerIndex].ToString(), segments));
											outStreams[tradePartnerIndex].Write(line.ToString());
											segmentCounter[tradePartnerIndex]++;
											ProcessHeader(ref sr, delimiter, fieldSeparator, rogueCrLf, crlf, ref outStreams, ref segmentCounter, ref plbStringBuilder, ref plbCounter, tradePartnerIndex, ref lxCounter, transactionDate, ref lxFound);
											initialST[tradePartnerIndex] = false;
										}
										else
										{
											SkipHeader(ref sr, delimiter[tradePartnerIndex], fieldSeparator[tradePartnerIndex], crlf[tradePartnerIndex], ref lxCounter, ref outStreams, ref plbStringBuilder, ref plbCounter, tradePartnerIndex, ref segmentCounter, ref lxFound);
											//segmentCounter[tradePartnerIndex]++;
										}
									}

									break;
								}
							case "SE*":
								{
										continue;
								}
							case "GS*":
								{
									if (zeroFileFound)
									{
										if (initialGS[tradePartnerIndex])
										{
											outStreams[tradePartnerIndex].Write(line.ToString());
											if (rogueCrLf[tradePartnerIndex])
											{
												sr.Read(charRead, 0, 1);
												sr.Read(charRead, 0, 1);
												charRead[0] = ' ';
											}
										}
										else
										{
											continue;
										}
									}
									break;
								}
							case "GE*":
								{
									if (zeroFileFound)
									{
										if (initialGS[tradePartnerIndex])
										{
											initialGS[tradePartnerIndex] = false;
											saveGE[tradePartnerIndex] = line.ToString();
										}
									}
									break;
								}
							case "PLB":
								{
									if (zeroFileFound)
									{
										string tempString = line.ToString();
										string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
										line.Length = 0;
										line.Append(String.Join(fieldSeparator[tradePartnerIndex].ToString(), segments));
										plbStringBuilder[tradePartnerIndex].Append(line.ToString());
										plbCounter[tradePartnerIndex]++;
									}

									break;
								}
							case "LX*":
								{
									if (zeroFileFound)
									{
										string tempString = line.ToString();
										string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
										if (crlf[tradePartnerIndex])
										{
											segments[1] = (++lxCounter[tradePartnerIndex]).ToString() + "\r\n";
										}
										else
										{
											segments[1] = (++lxCounter[tradePartnerIndex]).ToString() + delimiter[tradePartnerIndex];
										}
										line.Length = 0;
										line.Append(String.Join(fieldSeparator[tradePartnerIndex].ToString(), segments));
										outStreams[tradePartnerIndex].Write(line.ToString());
										segmentCounter[tradePartnerIndex]++;
										lxFound = true;
									}
									break;
								}
							case "CLP":
								{
									if (zeroFileFound)
									{
										if (lxFound == false)
										{
											string sep = string.Empty;
											if (crlf[tradePartnerIndex])
											{
												sep = (++lxCounter[tradePartnerIndex]).ToString() + "\r\n";
											}
											else
											{
												sep = (++lxCounter[tradePartnerIndex]).ToString() + delimiter[tradePartnerIndex];
											}
											outStreams[tradePartnerIndex].Write("LX" + fieldSeparator[tradePartnerIndex].ToString() + sep);
											segmentCounter[tradePartnerIndex]++;
										}
										outStreams[tradePartnerIndex].Write(line.ToString());
										if (rogueCrLf[tradePartnerIndex])
										{
											sr.Read(charRead, 0, 1);
											sr.Read(charRead, 0, 1);
											charRead[0] = ' ';
										}
										segmentCounter[tradePartnerIndex]++;
										lxFound = false;
									}
									else
									{
										string tempString = line.ToString();
										string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
										if (Convert.ToDecimal(segments[4]) == 0)
										{
											currentClaimWriteOffItem = new WriteOffItem("Claim");
											currentClaimWriteOffItem.itemAmount = segments[3];
											currentClaimWriteOffItem.encounterNumber = segments[1].Substring(2);
										}
									}
									break;
								}
							case "NM1":
								{
									string tempString = line.ToString();
									string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
									if (zeroFileFound)
									{
										outStreams[tradePartnerIndex].Write(line.ToString());
										if (rogueCrLf[tradePartnerIndex])
										{
											sr.Read(charRead, 0, 1);
											sr.Read(charRead, 0, 1);
											charRead[0] = ' ';
										}
										segmentCounter[tradePartnerIndex]++;
										if (segments[1] == "QC")
										{
											reportStreams[tradePartnerIndex].WriteLine(string.Format("Check Number: {0,-20}      Patient: {1,-35}     Transaction Date: {2,-8}", checkNumber, (segments[3] + "," + segments[4]), transactionDate));
										}
									}
									else
									{
										if ((segments[1] == "QC") && (string.Compare(currentClaimWriteOffItem.writeOffType,"Claim") == 0))
										{
											currentClaimWriteOffItem.lastName = segments[3];
											currentClaimWriteOffItem.firstName = segments[4];
											currentClaimWriteOffItem.middleName = segments[5];
										}
									}
									break;
								}
							default:
								{
									if (zeroFileFound)
									{
										outStreams[tradePartnerIndex].Write(line.ToString());
										if (rogueCrLf[tradePartnerIndex])
										{
											sr.Read(charRead, 0, 1);
											sr.Read(charRead, 0, 1);
											charRead[0] = ' ';
										}
										segmentCounter[tradePartnerIndex]++;
									}
									break;
								}
						}
					}
					sr.Close();
					if (zeroFileFound == false)
					{
						File.Copy(targetFile, targetFile.Replace("\\SourceFiles", string.Empty));
					}
					File.Move(targetFile,targetFile + ".Done");
				}
			}
			if (zeroFileFound)
			{
				for (int k = 0; k < segmentCounter.Count; k++)
				{
					outStreams[k].Write(plbStringBuilder[k].ToString());
					segmentCounter[k] = segmentCounter[k] + plbCounter[k];
					outStreams[k].Write("SE" + fieldSeparator[k] + ((++segmentCounter[k]).ToString()) + fieldSeparator[k] + "000000001" + delimiter[k]);
					outStreams[k].Write(saveGE[k]);
					outStreams[k].Write(saveIEA[k]);
				}
			}
			for (int k = 0; k < outStreams.Count; k++)
			{
				outStreams[k].Close();
				reportStreams[k].Close();
			}
		}

		private static void InitializeNewTradingPartner(string processName, string outputFolder, ref List<bool> initialISA, char tempDelimiter, char tempFieldSeparator, ref List<int> segmentCounter, ref List<int> plbCounter, int tradePartnerIndex, bool tempCrLf, bool tempRogueCrLf, ref List<StreamWriter> outStreams, ref List<StringBuilder> plbStringBuilder, List<string> tradePartners, ref List<bool> initialGS, ref List<bool> initialST, ref List<char> delimiter, ref List<char> fieldSeparator, ref List<string> saveIEA, ref List<string> saveGE, ref List<int> lxCounter, ref List<bool> crlf, ref List<bool> rogueCrLf, ref List<StreamWriter> reportStreams)
		{
			if (tradePartnerIndex == 0)
			{
				initialISA[0] = true;
			}
			initialISA.Add(true);
			initialGS.Add(true);
			initialST.Add(true);
			lxCounter.Add(-1);
			delimiter.Add(tempDelimiter);
			fieldSeparator.Add(tempFieldSeparator);
			crlf.Add(tempCrLf);
			rogueCrLf.Add(tempRogueCrLf);
			saveIEA.Add(string.Empty);
			saveGE.Add(string.Empty);
			outStreams.Add(new StreamWriter(outputFolder + processName + "_" + tradePartners[tradePartnerIndex].Trim() + "~" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".x12"));
			reportStreams.Add(new StreamWriter(outputFolder + processName + "_report_" + tradePartners[tradePartnerIndex].Trim() + "~" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"));
			segmentCounter.Add(0);
			plbCounter.Add(0);
			plbStringBuilder.Add(new StringBuilder());
		}

		private static int FindTradingPartner(char[] buffer, ref List<string> tradePartners, string transactionDate)
		{
			string tradePartnerID = new string(buffer, 35, 15).Trim();
			if (tradePartners.Count == 0)
			{
				tradePartners.Add(string.Concat(tradePartnerID, transactionDate));
				return  0;
			}
			else
			{
				for (int i = 0; i < tradePartners.Count; i++)
				{
					if (tradePartners[i] == string.Concat(tradePartnerID,transactionDate))
					{
						return i;
					}
				}
			}
			tradePartners.Add(string.Concat(tradePartnerID, transactionDate));
			return tradePartners.Count - 1;
		}

		private static bool ZeroDollarFile(string targetFile, ref char delimiter, ref char fieldSeparator, ref bool crlf, ref bool rogueCrLf)
		{
			char[] charRead = new char[1];
			StreamReader sr = new StreamReader(targetFile);
			char[] buffer = ParseISA(ref delimiter, ref fieldSeparator, ref crlf, ref rogueCrLf, charRead, sr);
			bool BPRNotChecked = true;
			bool retVal = false;
			while (BPRNotChecked)
			{
				StringBuilder line = ReadSegment(delimiter, sr);

				switch (line.ToString().Substring(0, 4))
				{
					case "BPR*":
						{
							BPRNotChecked = false;
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator);
							if (string.Compare(segments[2],"0.00") == 0)
							{
								retVal =  true;
							}
							break;
						}
					default:
						{
							break;
						}
				}
			}
			sr.Close();
			return retVal;
		}

		private static char[] ParseISA(ref char delimiter, ref char fieldSeparator, ref bool crlf, ref bool rogueCrLf, char[] charRead, StreamReader sr)
		{
			char[] buffer = new char[106];
			sr.Read(buffer, 0, 106);
			fieldSeparator = buffer[3];
			delimiter = buffer[105];
			if (delimiter == '\r')
			{
				crlf = true;
				delimiter = '\n';
				sr.Read(charRead, 0, 1);
			}
			else
			{
				if (sr.Peek() == 10)
				{
					sr.Read(charRead, 0, 1);
					rogueCrLf = true;
				}
			}
			return buffer;
		}

		private static void SkipHeader(ref StreamReader sr, char delimiter, char fieldSeparator, bool crlf, ref List<int> lxCounter, ref List<StreamWriter> outStreams, ref List<StringBuilder> plbStringBuilder, ref List<int> PLBCounter, int tradePartnerIndex, ref List<int> segmentCounter, ref bool lxFound)
		{
			bool headerNotProcessed = true;
			while (headerNotProcessed)
			{
				StringBuilder line = ReadSegment(delimiter, sr);

				switch (line.ToString().Substring(0, 3))
				{
					case "LX*":
						{
							headerNotProcessed = false;
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator);
							if (crlf)
							{
								segments[1] = (++lxCounter[tradePartnerIndex]).ToString() + "\r\n";
							}
							else
							{
								segments[1] = (++lxCounter[tradePartnerIndex]).ToString() + delimiter;
							}
							line.Length = 0;
							line.Append(String.Join((fieldSeparator.ToString()), segments));
							outStreams[tradePartnerIndex].Write(line.ToString());
							segmentCounter[tradePartnerIndex]++;
							lxFound = true;
							
							break;
						}
					case "PLB":
						{
							headerNotProcessed = false;
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator);
							line.Length = 0;
							line.Append(String.Join(fieldSeparator.ToString(), segments));
							plbStringBuilder[tradePartnerIndex].Append(line.ToString());
							PLBCounter[tradePartnerIndex]++;

							break;
						}
					default:
						{
							break;
						}
				}
			}
		}

		private static StringBuilder ReadSegment(char delimiter, StreamReader sr)
		{
			char[] charRead = new char[1];					
			string segmentString  = string.Empty;
			charRead[0] = ' ';
			StringBuilder line = new StringBuilder();
			while (charRead[0].ToString() != delimiter.ToString())
			{
				sr.Read(charRead,0,1);
				line.Append(charRead[0]);
			}
			return line;
		}

		private static void ProcessHeader(ref StreamReader sr, List<char> delimiter, List<char> fieldSeparator, List<bool> rogueCrLf, List<bool> crlf, ref List<StreamWriter> outStreams, ref List<int> segmentCounter, ref List<StringBuilder> plbStringBuilder, ref List<int> currentPLBCounter, int tradePartnerIndex, ref List<int> lxCounter, string transactionDate, ref bool lxFound)
		{
			bool headerNotProcessed = true;
			StringBuilder savedLine = new StringBuilder();		//SavedLine stringbuilder saves all lines from the header until an LX or PLB is located and then the full header is written to the output file
			while (headerNotProcessed)
			{
				StringBuilder line = ReadSegment(delimiter[tradePartnerIndex], sr);

				switch (line.ToString().Substring(0, 3))
				{
					case "TRN":
						{
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
							segments[2] = "?PayerName? " + transactionDate;
							line.Length = 0;
							line.Append(String.Join(fieldSeparator[tradePartnerIndex].ToString(), segments));
							savedLine.Append(line.ToString());
							segmentCounter[tradePartnerIndex]++;
							break;
						}
					case "N1*":
						{
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
							if (string.Compare("PR", segments[1]) == 0)
							{
								savedLine.Replace("?PayerName?", segments[2]);
							}
							savedLine.Append(line.ToString());
							segmentCounter[tradePartnerIndex]++;
							break;
						}
					case "LX*":
						{
							headerNotProcessed = false;
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
							if (crlf[tradePartnerIndex])
							{
								segments[1] = (++lxCounter[tradePartnerIndex]).ToString() + "\r\n";
							}
							else
							{
								segments[1] = (++lxCounter[tradePartnerIndex]).ToString() + delimiter[tradePartnerIndex];
							}
							line.Length = 0;
							line.Append(String.Join(fieldSeparator[tradePartnerIndex].ToString(), segments));
							savedLine.Append(line.ToString());
							outStreams[tradePartnerIndex].Write(savedLine.ToString());
							segmentCounter[tradePartnerIndex]++;
							lxFound = true;
							break;
						}
					case "PLB":
						{
							headerNotProcessed = false;
							string tempString = line.ToString();
							string[] segments = tempString.Split(fieldSeparator[tradePartnerIndex]);
							plbStringBuilder[tradePartnerIndex].Append(String.Join(fieldSeparator.ToString(), segments));
							currentPLBCounter[tradePartnerIndex]++;
							outStreams[tradePartnerIndex].Write(savedLine.ToString());		// Write out the header to outStreams, but we will save and not write the currentPLB until all processing is complete and we are closing all of the output files.
							break;
						}
					default:
						{
							savedLine.Append(line.ToString());
							if (rogueCrLf[tradePartnerIndex])
							{
								char[] charRead = new char[1];
								sr.Read(charRead, 0, 1);
								sr.Read(charRead, 0, 1);
							}
							segmentCounter[tradePartnerIndex]++;
							break;
						}
				}
			}
		}
	}
}
