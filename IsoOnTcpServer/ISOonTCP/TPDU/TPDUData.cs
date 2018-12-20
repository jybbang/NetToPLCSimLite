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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HexDumping;

namespace IsoOnTcp
{
    public class TPDUData
    {
        public const int HLEN_LI_NORMAL_DT_CLASS_01 = 2;  // Header length indicator of fixed part when class 0 or 1
        public const int DT_HLEN = 3;                     // Header length of a DT packet (incl. LI)

        public int TPDUNr;      // Send sequence number
        public bool EOT;        // This is last data unit
        public byte[] Payload;  // payload of DT TPDU packet
        public int PayloadLength;

        public TPDUData()
        { }

        public TPDUData(byte[] packet)
            : this(packet, packet.Length)
        {
        }

        public TPDUData(byte[] packet, int packetLen)
        {
            if (packetLen < DT_HLEN)
                throw new Exception("TPDU: DT packet size lower than minimum of 3 bytes.");

            int li = packet[0];
            TPDU.TPDU_TYPES type = (TPDU.TPDU_TYPES)(packet[1] >> 4);

            if (type != TPDU.TPDU_TYPES.DT)
                throw new ApplicationException("TPDU: This can only handle DT TDPUs");

            if (li != HLEN_LI_NORMAL_DT_CLASS_01)
                throw new Exception("TPDU: Header length indicator in DT packet other than 2 in class 0/1 PDUs are not allowed.");

            TPDUNr = (packet[2] & 0x7f);

            if ((packet[2] & 0x80) != 0)
                EOT = true;
            else
                EOT = false;

            PayloadLength = packetLen - DT_HLEN;
            Payload = new byte[PayloadLength];
            Array.Copy(packet, DT_HLEN, Payload, 0, PayloadLength);
        }
    }
}
