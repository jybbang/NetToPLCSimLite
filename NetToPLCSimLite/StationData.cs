/*********************************************************************
 * NetToPLCsim, Netzwerkanbindung fuer PLCSIM
 * 
 * Copyright (C) 2011-2016 Thomas Wiens, th.wiens@gmx.de
 *
 * This file is part of NetToPLCsim.
 *
 * NetToPLCsim is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 /*********************************************************************/

using System;
using System.Net;

namespace NetToPLCSim
{
    public class StationData
    {        
        public StationData()
        {
            Name = String.Empty;
            NetworkIpAddress = IPAddress.None;
            PlcsimIpAddress = IPAddress.None;
            PlcsimRackNumber = 0;
            PlcsimSlotNumber = 3;
            TsapCheckEnabled = false;
            Status = StationStatus.READY.ToString();
        }

        public StationData(string name, IPAddress networkIpAddress, IPAddress plcsimIpAddress, int rack, int slot, bool tsapCheckEnabled)
        {
            Name = name;
            NetworkIpAddress = networkIpAddress;
            PlcsimIpAddress = plcsimIpAddress;
            PlcsimRackNumber = rack;
            PlcsimSlotNumber = slot;
            TsapCheckEnabled = tsapCheckEnabled;
            Status = StationStatus.READY.ToString();
        }

        public string Name { get; set; }    
        public IPAddress NetworkIpAddress { get; set; }    
        public IPAddress PlcsimIpAddress { get; set; }    
        public int PlcsimRackNumber { get; set; }    
        public int PlcsimSlotNumber { get; set; }    
        public string PlcsimRackSlot { get { return PlcsimRackNumber.ToString() + "/" + PlcsimSlotNumber.ToString(); } }
        public bool TsapCheckEnabled { get; set; }    
        public bool Connected { get; set; }    
        public string Status { get; set; }    
        
        public StationData ShallowCopy()
        {
            return (StationData)this.MemberwiseClone();
        }
    }
}
