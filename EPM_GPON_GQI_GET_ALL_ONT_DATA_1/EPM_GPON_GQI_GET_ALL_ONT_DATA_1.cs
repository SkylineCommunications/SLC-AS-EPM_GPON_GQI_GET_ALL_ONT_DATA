/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		AGA, Skyline	Initial version
****************************************************************************
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using SLDataGateway.API.Types.Results.Paging;

[GQIMetaData(Name = "All ONT Data")]
public class CmData : IGQIDataSource, IGQIInputArguments, IGQIOnInit
{
	private readonly GQIStringArgument frontEndElementArg = new GQIStringArgument("FE Element")
	{
		IsRequired = true,
	};

	private readonly GQIStringArgument filterEntityArg = new GQIStringArgument("Filter Entity")
	{
		IsRequired = false,
	};

	private readonly GQIStringArgument entityBeTablePidArg = new GQIStringArgument("BE Entity Table PID")
	{
		IsRequired = true,
	};

	private readonly GQIStringArgument entityBeOltsIdsArg = new GQIStringArgument("Entity OLT Dma/Eid IDX")
	{
		IsRequired = true,
	};

	private readonly GQIBooleanArgument onlyShowOosOntsArg = new GQIBooleanArgument("Only Show OOS ONTs")
	{
		IsRequired = true,
		DefaultValue = true,
	};

	private GQIDMS _dms;

	private string frontEndElement = String.Empty;

	private string filterEntity = String.Empty;

	private int entityBeTablePid = 0;

	private int entityBeOltsIdx = 0;

	private int backendRegistrationTable = 1500050;

	private bool onlyOosOnts = false;

	private List<GQIRow> listGqiRows = new List<GQIRow> { };

	public OnInitOutputArgs OnInit(OnInitInputArgs args)
	{
		_dms = args.DMS;
		return new OnInitOutputArgs();
	}

	public GQIArgument[] GetInputArguments()
	{
		return new GQIArgument[]
		{
			frontEndElementArg,
			filterEntityArg,
			entityBeTablePidArg,
			entityBeOltsIdsArg,
			onlyShowOosOntsArg,
		};
	}

	public GQIColumn[] GetColumns()
	{
		return new GQIColumn[]
		{
			new GQIStringColumn("ONT ID"),
			new GQIStringColumn("ONT Serial Number"),
			new GQIStringColumn("Contract ID"),
			new GQIStringColumn("Main Street"),
			new GQIStringColumn("Street 1"),
			new GQIStringColumn("Street 2"),
			new GQIStringColumn("Neighborhood"),
			new GQIStringColumn("Description"),
			new GQIStringColumn("Hardware Version"),
			new GQIStringColumn("Manufacturer"),
			new GQIStringColumn("Software Version"),
			new GQIDoubleColumn("Uptime"),
			new GQIDoubleColumn("CPU"),
			new GQIDoubleColumn("Memory"),
			new GQIIntColumn("State"),
			new GQIDoubleColumn("BIAS Current"),
			new GQIDoubleColumn("Supply Voltage"),
			new GQIDoubleColumn("Rx Power"),
			new GQIDoubleColumn("Tx Power"),
			new GQIDoubleColumn("Transceiver Temperature"),
			new GQIIntColumn("BIAS Current State"),
			new GQIIntColumn("Supply Voltage State"),
			new GQIIntColumn("Rx Power State"),
			new GQIIntColumn("Tx Power State"),
			new GQIIntColumn("Transceiver Temperature State"),
			new GQIDateTimeColumn("Last Data Update"),

		};
	}

	public GQIPage GetNextPage(GetNextPageInputArgs args)
	{
		return new GQIPage(listGqiRows.ToArray())
		{
			HasNextPage = false,
		};
	}

	public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
	{
		listGqiRows.Clear();
		try
		{
			frontEndElement = args.GetArgumentValue(frontEndElementArg);
			filterEntity = args.GetArgumentValue(filterEntityArg);
			entityBeTablePid = Convert.ToInt32(args.GetArgumentValue(entityBeTablePidArg));
			entityBeOltsIdx = Convert.ToInt32(args.GetArgumentValue(entityBeOltsIdsArg));
			onlyOosOnts = Convert.ToBoolean(args.GetArgumentValue(onlyShowOosOntsArg));
			var backEndHelper = GetBackEndElement();
			if (backEndHelper == null)
			{
				return new OnArgumentsProcessedOutputArgs();
			}
			List<HelperPartialSettings[]> relatedOnts = GetSerialNumbers(backEndHelper);
			var macFilter = GetRelatedSerialFitler(4002, relatedOnts);
			var macSubscriberFilter = GetRelatedSerialFitler(6010, relatedOnts);

			if (String.IsNullOrEmpty(macFilter))
			{
				return new OnArgumentsProcessedOutputArgs();
			}

			var ccapTableOntInformation = GetTable(Convert.ToString(backEndHelper.OltId), 4000, new List<string>
			{
				macFilter,
			});
			var ccapTableSubscriberInformation = GetTable(Convert.ToString(backEndHelper.OltId), 6000, new List<string>
			{
				macSubscriberFilter,
			});

			Dictionary<string, OltOverview> oltsRows = ExtractOltData(ccapTableOntInformation);
			Dictionary<string, SubscriberOverview> oltsSubscriberRows = ExtractSubscriberData(ccapTableSubscriberInformation);

			if (onlyOosOnts)
			{
				AddOnlyOssOnt(oltsRows, oltsSubscriberRows);
			}
			else
			{
				AddAllOnts(oltsRows, oltsSubscriberRows);
			}
		}
		catch
		{
			listGqiRows = new List<GQIRow>();
		}

		return new OnArgumentsProcessedOutputArgs();
	}

	private List<HelperPartialSettings[]> GetSerialNumbers(BackEndHelper backEndHelper)
	{
		return GetTable(backEndHelper.ElementId, 1513000, new List<string>
			{
				String.Format("forceFullTable=true;fullFilter=({0}=={1})", entityBeTablePid + 1, backEndHelper.EntityId),
			});
	}

	public List<HelperPartialSettings[]> GetTable(string element, int tableId, List<string> filter)
	{
		var columns = new List<HelperPartialSettings[]>();

		var elementIds = element.Split('/');
		if (elementIds.Length > 1 && Int32.TryParse(elementIds[0], out int dmaId) && Int32.TryParse(elementIds[1], out int elemId))
		{
			// Retrieve client connections from the DMS using a GetInfoMessage request
			var getPartialTableMessage = new GetPartialTableMessage(dmaId, elemId, tableId, filter.ToArray());
			var paramChange = (ParameterChangeEventMessage)_dms.SendMessage(getPartialTableMessage);

			if (paramChange != null && paramChange.NewValue != null && paramChange.NewValue.ArrayValue != null)
			{
				columns = paramChange.NewValue.ArrayValue
					.Where(av => av != null && av.ArrayValue != null)
					.Select(p => p.ArrayValue.Where(v => v != null)
					.Select(c => new HelperPartialSettings
					{
						CellValue = c.CellValue.InteropValue,
						DisplayValue = c.CellValue.CellDisplayValue,
						DisplayType = c.CellDisplayState,
					}).ToArray()).ToList();
			}
		}

		return columns;
	}

	public BackEndHelper GetBackEndElement()
	{
		if (String.IsNullOrEmpty(filterEntity))
		{
			return null;
		}

		var backendTable = GetTable(frontEndElement, backendRegistrationTable, new List<string>
		{
			"forceFullTable=true",
		});


		if (backendTable != null && backendTable.Any())
		{
			for (int i = 0; i < backendTable[0].Count(); i++)
			{
				var key = Convert.ToString(backendTable[0][i].CellValue);

				var backendEntityTable = GetTable(key, entityBeTablePid, new List<string>
				{
					String.Format("forceFullTable=true;fullFilter=({0}=={1})", entityBeTablePid + 2, filterEntity),
				});

				if (backendEntityTable != null && backendEntityTable.Any() && backendEntityTable[0].Length > 0)
				{

					return new BackEndHelper
					{
						ElementId = key,
						OltId = Convert.ToString(backendEntityTable[entityBeOltsIdx][0].CellValue),
						EntityId = Convert.ToString(backendEntityTable[0][0].CellValue),
					};
				}
			}
		}

		return null;
	}

	public string GetRelatedSerialFitler(int comparisonValue, List<HelperPartialSettings[]> relatedOnts)
	{
		var macFilter = new StringBuilder("forceFullTable=true;fullFilter=");
		if (relatedOnts != null && relatedOnts.Any() && relatedOnts[0].Length > 0)
		{
			for (int i = 0; i < relatedOnts[0].Count(); i++)
			{
				var ontSerial = Convert.ToString(relatedOnts[1][i].CellValue);

				if (!String.IsNullOrEmpty(ontSerial))
				{
					macFilter.Append(string.Format("({0}=={1}) OR ", comparisonValue, ontSerial));
				}
			}

			return macFilter.ToString().TrimEnd(new[] { ' ', 'O', 'R' });
		}

		return String.Empty;
	}

	public static string ParseState(int state)
	{
		switch (state)
		{
			case 0:
				return "Offline";

			case 1:
				return "Online";

			default:
				return "No Reported Data";
		}
	}

	public static string ParseUptime(double uptime)
	{
		if (uptime.Equals(-999))
		{
			return "No Reported Data";
		}
		else if (uptime.Equals(-1))
		{
			return "N/A";
		}
		else
		{
			var uptimeSpan = TimeSpan.FromSeconds(uptime);
			return String.Format("{0} days {1}h {2}m {3}s", uptimeSpan.Days, uptimeSpan.Hours, uptimeSpan.Minutes, uptimeSpan.Seconds);
		}
	}

	public static string ParseDateTimeString(double dateTime)
	{
		if (dateTime.Equals(-999))
		{
			return "No Reported Data";
		}

		return DateTime.FromOADate(dateTime).ToUniversalTime().ToString();
	}

	public static DateTime ParseDateTime(double dateTime)
	{
		if (dateTime.Equals(-999))
		{
			return DateTime.MinValue.ToUniversalTime();
		}

		return DateTime.FromOADate(dateTime).ToUniversalTime();
	}

	public static string ParseStatus(int statusValue)
	{
		switch (statusValue)
		{
			case 1:
				return "OK";

			case 2:
				return "OOS";

			default:
				return "No Reported Data";
		}
	}

	public static string ParseDoubleValue(double doubleValue, string unit)
	{
		if (doubleValue.Equals(-999))
		{
			return "No Reported Data";
		}

		return Math.Round(doubleValue, 2) + " " + unit;
	}

	public static string ParseStringValue(string stringValue)
	{
		if (String.IsNullOrEmpty(stringValue) || stringValue == "-999")
		{
			return "No Reported Data";
		}

		return stringValue;
	}

	private Dictionary<string, OltOverview> ExtractOltData(List<HelperPartialSettings[]> oltTable)
	{
		Dictionary<string, OltOverview> oltRows = new Dictionary<string, OltOverview>();
		if (oltTable != null && oltTable.Any())
		{
			for (int i = 0; i < oltTable[0].Count(); i++)
			{
				var key = Convert.ToString(oltTable[0][i].CellValue);
				var oltRow = new OltOverview
				{
					OntId = key,
					SerialNumber = Convert.ToString(oltTable[1][i].CellValue),
					Description = Convert.ToString(oltTable[10][i].CellValue),
					HardwareVersion = Convert.ToString(oltTable[11][i].CellValue),
					Manufacturer = Convert.ToString(oltTable[12][i].CellValue),
					SoftwareVersion = Convert.ToString(oltTable[13][i].CellValue),
					Uptime = Convert.ToDouble(oltTable[14][i].CellValue),
					Cpu = Convert.ToDouble(oltTable[15][i].CellValue),
					Memory = Convert.ToDouble(oltTable[16][i].CellValue),
					State = Convert.ToInt32(oltTable[17][i].CellValue),
					BiasCurrent = Convert.ToDouble(oltTable[18][i].CellValue),
					SupplyVoltage = Convert.ToDouble(oltTable[19][i].CellValue),
					RxPower = Convert.ToDouble(oltTable[20][i].CellValue),
					TxPower = Convert.ToDouble(oltTable[21][i].CellValue),
					TransceiverTemperature = Convert.ToDouble(oltTable[22][i].CellValue),
					BiasCurrentState = Convert.ToInt32(oltTable[23][i].CellValue),
					SupplyVoltageState = Convert.ToInt32(oltTable[24][i].CellValue),
					RxPowerState = Convert.ToInt32(oltTable[25][i].CellValue),
					TxPowerState = Convert.ToInt32(oltTable[26][i].CellValue),
					TransceiverTemperatureState = Convert.ToInt32(oltTable[27][i].CellValue),
					LastDataUpdate = Convert.ToDouble(oltTable[28][i].CellValue),
				};

				oltRows[key] = oltRow;
			}
		}

		return oltRows;
	}

	private Dictionary<string, SubscriberOverview> ExtractSubscriberData(List<HelperPartialSettings[]> oltTable)
	{
		Dictionary<string, SubscriberOverview> oltRows = new Dictionary<string, SubscriberOverview>();

		if (oltTable != null && oltTable.Any())
		{

			for (int i = 0; i < oltTable[0].Count(); i++)
			{
				var key = Convert.ToString(oltTable[0][i].CellValue);
				var oltRow = new SubscriberOverview
				{
					OntId = key,
					ContractId = Convert.ToString(oltTable[1][i].CellValue),
					SerialNumber = Convert.ToString(oltTable[0][i].CellValue),
					MainStreet = Convert.ToString(oltTable[5][i].CellValue),
					Street1 = Convert.ToString(oltTable[7][i].CellValue),
					Street2 = Convert.ToString(oltTable[8][i].CellValue),
					Neighborhood = Convert.ToString(oltTable[4][i].CellValue),
				};

				oltRows[key] = oltRow;
			}
		}

		return oltRows;
	}

	private void AddAllOnts(Dictionary<string, OltOverview> oltRows, Dictionary<string, SubscriberOverview> OltSubscriberRows)
	{
		foreach (var oltRow in oltRows.Values)
		{
			var subscriberInformation = OltSubscriberRows.ContainsKey(oltRow.OntId) ? OltSubscriberRows[oltRow.OntId]: new SubscriberOverview();

			List<GQICell> listGqiCells = new List<GQICell>
				{
					new GQICell
					{
						Value = oltRow.OntId,
					},
					new GQICell
					{
						Value = oltRow.SerialNumber ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.ContractId ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.MainStreet ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.Street1 ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.Street2 ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.Neighborhood ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.Description),
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.HardwareVersion),
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.Manufacturer),
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.SoftwareVersion),
					},
					new GQICell
					{
						Value = oltRow.Uptime,
						DisplayValue = ParseUptime(oltRow.Uptime),
					},
					new GQICell
					{
						Value = oltRow.Cpu,
						DisplayValue = ParseDoubleValue(oltRow.Cpu, "%"),
					},
					new GQICell
					{
						Value = oltRow.Memory,
						DisplayValue = ParseDoubleValue(oltRow.Memory, "%"),
					},
					new GQICell
					{
						Value = oltRow.State,
						DisplayValue = ParseState(oltRow.State),
					},
					new GQICell
					{
						Value = oltRow.BiasCurrent,
						DisplayValue = ParseDoubleValue(oltRow.BiasCurrent, "mA"),
					},
					new GQICell
					{
						Value = oltRow.SupplyVoltage,
						DisplayValue = ParseDoubleValue(oltRow.SupplyVoltage, "V"),
					},
					new GQICell
					{
						Value = oltRow.RxPower,
						DisplayValue = ParseDoubleValue(oltRow.RxPower, "dBm"),
					},
					new GQICell
					{
						Value = oltRow.TxPower,
						DisplayValue = ParseDoubleValue(oltRow.TxPower, "dBm"),
					},
					new GQICell
					{
						Value = oltRow.TransceiverTemperature,
						DisplayValue = ParseDoubleValue(oltRow.TransceiverTemperature, "deg C"),
					},
					new GQICell
					{
						Value = oltRow.BiasCurrentState,
						DisplayValue = ParseStatus(oltRow.BiasCurrentState),
					},
					new GQICell
					{
						Value = oltRow.SupplyVoltageState,
						DisplayValue = ParseStatus(oltRow.SupplyVoltageState),
					},
					new GQICell
					{
						Value = oltRow.RxPowerState,
						DisplayValue = ParseStatus(oltRow.RxPowerState),
					},
					new GQICell
					{
						Value = oltRow.TxPowerState,
						DisplayValue = ParseStatus(oltRow.TxPowerState),
					},
					new GQICell
					{
						Value = oltRow.TransceiverTemperatureState,
						DisplayValue = ParseStatus(oltRow.TransceiverTemperatureState),
					},
					new GQICell
					{
						Value = ParseDateTime(oltRow.LastDataUpdate),
						DisplayValue = ParseDateTimeString(oltRow.LastDataUpdate),
					},
				};
			var gqiRow = new GQIRow(listGqiCells.ToArray());

			listGqiRows.Add(gqiRow);

		}

	}

	private void AddOnlyOssOnt(Dictionary<string, OltOverview> oltRows, Dictionary<string, SubscriberOverview> OltSubscriberRows)
	{
		foreach (var oltRow in oltRows.Values)
		{
			if (oltRow.BiasCurrentState == (int)OltState.Oos || oltRow.SupplyVoltageState == (int)OltState.Oos || oltRow.RxPowerState == (int)OltState.Oos || oltRow.TxPowerState == (int)OltState.Oos || oltRow.TransceiverTemperatureState == (int)OltState.Oos)
			{
				var subscriberInformation = OltSubscriberRows.ContainsKey(oltRow.OntId) ? OltSubscriberRows[oltRow.OntId] : new SubscriberOverview();

				List<GQICell> listGqiCells = new List<GQICell>
				{
					new GQICell
					{
						Value = oltRow.OntId,
					},
					new GQICell
					{
						Value = oltRow.SerialNumber ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.ContractId ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.MainStreet ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.Street1 ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.Street2 ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = subscriberInformation.Neighborhood ?? subscriberInformation.Exception,
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.Description),
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.HardwareVersion),
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.Manufacturer),
					},
					new GQICell
					{
						Value = ParseStringValue(oltRow.SoftwareVersion),
					},
					new GQICell
					{
						Value = oltRow.Uptime,
						DisplayValue = ParseUptime(oltRow.Uptime),
					},
					new GQICell
					{
						Value = oltRow.Cpu,
						DisplayValue = ParseDoubleValue(oltRow.Cpu, "%"),
					},
					new GQICell
					{
						Value = oltRow.Memory,
						DisplayValue = ParseDoubleValue(oltRow.Memory, "%"),
					},
					new GQICell
					{
						Value = oltRow.State,
						DisplayValue = ParseState(oltRow.State),
					},
					new GQICell
					{
						Value = oltRow.BiasCurrent,
						DisplayValue = ParseDoubleValue(oltRow.BiasCurrent, "mA"),
					},
					new GQICell
					{
						Value = oltRow.SupplyVoltage,
						DisplayValue = ParseDoubleValue(oltRow.SupplyVoltage, "V"),
					},
					new GQICell
					{
						Value = oltRow.RxPower,
						DisplayValue = ParseDoubleValue(oltRow.RxPower, "dBm"),
					},
					new GQICell
					{
						Value = oltRow.TxPower,
						DisplayValue = ParseDoubleValue(oltRow.TxPower, "dBm"),
					},
					new GQICell
					{
						Value = oltRow.TransceiverTemperature,
						DisplayValue = ParseDoubleValue(oltRow.TransceiverTemperature, "deg C"),
					},
					new GQICell
					{
						Value = oltRow.BiasCurrentState,
						DisplayValue = ParseStatus(oltRow.BiasCurrentState),
					},
					new GQICell
					{
						Value = oltRow.SupplyVoltageState,
						DisplayValue = ParseStatus(oltRow.SupplyVoltageState),
					},
					new GQICell
					{
						Value = oltRow.RxPowerState,
						DisplayValue = ParseStatus(oltRow.RxPowerState),
					},
					new GQICell
					{
						Value = oltRow.TxPowerState,
						DisplayValue = ParseStatus(oltRow.TxPowerState),
					},
					new GQICell
					{
						Value = oltRow.TransceiverTemperatureState,
						DisplayValue = ParseStatus(oltRow.TransceiverTemperatureState),
					},
					new GQICell
					{
						Value = ParseDateTime(oltRow.LastDataUpdate),
						DisplayValue = ParseDateTimeString(oltRow.LastDataUpdate),
					},

				};
				
				var gqiRow = new GQIRow(listGqiCells.ToArray());

				listGqiRows.Add(gqiRow);
			}
		}
	}
}

enum OltState
{
	Oos = 2,
}

public class BackEndHelper
{
	public string ElementId { get; set; }

	public string OltId { get; set; }

	public string EntityId { get; set; }
}

public class HelperPartialSettings
{
	public object CellValue { get; set; }

	public object DisplayValue { get; set; }

	public ParameterDisplayType DisplayType { get; set; }
}

public class OltOverview
{
	public string OntId { get; set; }

	public string SerialNumber { get; set; }

	public string Description { get; set; }

	public string HardwareVersion { get; set; }

	public string Manufacturer { get; set; }

	public string SoftwareVersion { get; set; }

	public double Uptime { get; set; }

	public double Cpu { get; set; }

	public double Memory { get; set; }

	public int State { get; set; }

	public double BiasCurrent { get; set; }

	public double SupplyVoltage { get; set; }

	public double RxPower { get; set; }

	public double TxPower { get; set; }

	public double TransceiverTemperature { get; set; }

	public int BiasCurrentState { get; set; }

	public int SupplyVoltageState { get; set; }

	public int RxPowerState { get; set; }

	public int TxPowerState { get; set; }

	public int TransceiverTemperatureState { get; set; }

	public double LastDataUpdate { get; set; }
}

public class SubscriberOverview
{
	public string OntId { get; set; }

	public string SerialNumber { get; set; }

	public string ContractId { get; set; }

	public string MainStreet { get; set; }

	public string Street1 { get; set; }

	public string Street2 { get; set; }

	public string Neighborhood { get; set; }

	public string Exception { get; set; }

	public SubscriberOverview()
	{
		Exception = "N/A";
	}
}