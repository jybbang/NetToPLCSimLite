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

using System.Collections.Generic;

namespace NetToPLCSim
{
    public class Config
    {
        public List<StationData> Stations = new List<StationData>();

        public bool IsStationNameUnique(string name)
        {
            foreach (StationData st in Stations)
            {
                if (st.Name == name)
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsStationNameUniqueExcept(string name, int index)
        {
            for (int i = 0; i < Stations.Count; i++)
            {
                if (i != index)
                {
                    if (Stations[i].Name == name)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }    
}
