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
using System.Collections;

namespace IsoOnTcp
{
    public class TPDU
    {
        public enum TPDU_TYPES
        {
            ED = 0x1,       // ED Expedited Data
            EA = 0x2,       // EA Expedited Data Acknowledgement
            UD = 0x4,       // cltp UD
            RJ = 0x5,       // RJ Reject
            AK = 0x6,       // AK Data Acknowledgement
            ER = 0x7,       // ER TPDU Error
            DR = 0x8,       // DR Disconnect Request
            DC = 0xC,       // DC Disconnect Confirm
            CC = 0xD,       // CC Connect Confirm
            CR = 0xE,       // CR Connect Request
            DT = 0xF        // DT Data
        };

        // fixed part
        public UInt16 Li;           // Length indicator, 1 Byte
        public int PDUType;         // PDU Type/Code, 4 Bits
        // public UInt16 Cdt;          // Cdt, 4 Bits

        public TPDUData PduData;
        public TPDUConnection PduCon;

        public TPDU()
        { }

        /// <summary>
        /// Parses a a TDPU telegram byte array, throws exception when no correct TDPU telegram
        /// </summary>
        /// <param name="data">byte array of telegram data (payload of TPKT)</param>
        ///
        public TPDU(byte[] packet)
            : this(packet, packet.Length)
        {
        }

        private TPDU(byte[] packet, int packetLen)
        {

            if (packetLen < 3)
                throw new Exception("TPUD: Packet size lower than 3 bytes.");

            Li = packet[0];
            PDUType = (packet[1] >> 4);

            switch (PDUType)
            {
                case (int)TPDU_TYPES.CR:
                    PduCon = new TPDUConnection(packet);
                    break;
                case (int)TPDU_TYPES.CC:
                    PduCon = new TPDUConnection(packet);
                    break;
                case (int)TPDU_TYPES.DT:
                    PduData = new TPDUData(packet);
                    break;
            }
        }

        public byte[] GetBytes()
        {
            int size = 0;
            byte[] tpdu = null;
            switch (PDUType)
            {
                case (int)TPDU_TYPES.DT:
                    size = PduData.PayloadLength + TPDUData.DT_HLEN;
                    tpdu = new byte[size];
                    Li = 2;
                    tpdu[0] = Convert.ToByte(Li);
                    tpdu[1] = Convert.ToByte(PDUType << 4);
                    tpdu[2] = Convert.ToByte(PduData.TPDUNr);
                    if (PduData.EOT)
                       tpdu[2] += 128;

                    Array.Copy(PduData.Payload, 0, tpdu, TPDUData.DT_HLEN, PduData.Payload.Length);
                    break;
                case (int)TPDU_TYPES.CC:
                case (int)TPDU_TYPES.CR:
                    byte[] pduc;
                    pduc = PduCon.GetBytes();
                    int len = pduc.Length + 2;
                    Li = Convert.ToByte(pduc.Length + 1);
                    tpdu = new byte[len];
                    tpdu[0] = Convert.ToByte(Li);
                    tpdu[1] = Convert.ToByte(PDUType << 4);
                    Array.Copy(pduc, 0, tpdu, 2, pduc.Length);
                    break;
            }
            return tpdu;
        }

        public void MakeDataPacket(byte[] data)
        {
            MakeDataPacket(data, 0, data.Length, true);
        }

        public void MakeDataPacket(byte[] data, int sourceIndex, int length, bool lastDataUnit)
        {
            PDUType = (int)TPDU_TYPES.DT;
            Li = 2;
            PduData = new TPDUData();
            PduData.TPDUNr = 0;
            PduData.EOT = lastDataUnit;
            PduData.Payload = new byte[length];
            PduData.PayloadLength = length;
            Array.Copy(data, sourceIndex, PduData.Payload, 0, length);
        }
    }
}
