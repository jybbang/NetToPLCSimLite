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
using System.Collections;
using System.Text;
using System.IO;
using HexDumping;
using TcpLib;

namespace IsoOnTcp
{
    public class ISOonTCP
    {
        public bool Connected;

        private int m_ProposedMaxTPDUSize = 1024;  // Proposed Max TPDU size in bytes
        private int m_MaxTPDUSize;                 // Negotiated TPDU size in bytes

        public List<byte[]> LocalTsaps;
        public bool EnableLocalTsapCheck;

        public _OnReceived OnReceived;
        public _TCPSend TCPSend;
        public _Log Log;

        public delegate void _OnReceived(IsoServiceProvider client, byte[] data);
        public delegate void _TCPSend(ConnectionState state, byte[] data);
        public delegate void _Log(string message);

        private MemoryStream m_payloadFragmentData;

        public void Process(IsoServiceProvider client, byte[] packet)
        {
            Process(client, packet, packet.Length);
        }

        public void SetValidTsaps(List<byte[]> tsaps)
        {
            LocalTsaps = tsaps;
        }

        public void Process(IsoServiceProvider client, byte[] inPacket, int len)
        {
            int workedLen;
            TPKT PKT;
            TPDU PDU;
            byte[] packet = new byte[len];
            Array.Copy(inPacket, packet, len);
            workedLen = 0;
            while (workedLen < len)
            {
                PKT = new TPKT(packet, len);
                PDU = new TPDU(PKT.Payload);

                switch (PDU.PDUType)
                {
                    case (byte)TPDU.TPDU_TYPES.CR:
                        if (Connected)
                        {
                            break;
                        }

                        TPKT resPkt = new TPKT();
                        TPDU resPdu = new TPDU();
                        resPdu.PDUType = (byte)TPDU.TPDU_TYPES.CC;
                        Connected = false;
                        m_payloadFragmentData = new MemoryStream(1024);    // Stream for TPDU fragments, S7 PDU has max. 960 bytes

                        // get connect confirm, if tsaps not equal a exception is thrown
                        try
                        {
                            resPdu.PduCon = PDU.PduCon.HandleConnectRequest(LocalTsaps, m_ProposedMaxTPDUSize, EnableLocalTsapCheck);
                            // Add TPDU to TPKT
                            resPkt.SetPayload(resPdu.GetBytes());
                            // send connect confirm
                            TCPSend(client.client, resPkt.GetBytes());
                            Connected = true;
                            m_MaxTPDUSize = resPdu.PduCon.GetMaxTPDUSize();
                        }
                        catch {

                        }
                        break;
                    case (byte)TPDU.TPDU_TYPES.DT:
                        if (!Connected)
                        {
                            Log("DT packet before state 'connected' received.");
                            m_payloadFragmentData.SetLength(0);
                            break;
                        }
                        // Handle DT TPDU fragments              
                        if (PDU.PduData.EOT == false)
                        {
                            m_payloadFragmentData.Write(PDU.PduData.Payload, 0, PDU.PduData.Payload.Length);
                        }
                        else
                        {
                            if (m_payloadFragmentData.Length == 0)
                            {
                                OnReceived(client, PDU.PduData.Payload);
                            }
                            else
                            {
                                m_payloadFragmentData.Write(PDU.PduData.Payload, 0, PDU.PduData.Payload.Length);
                                byte[] payload = new byte[m_payloadFragmentData.Length];
                                payload = m_payloadFragmentData.ToArray();
                                OnReceived(client, payload);
                                m_payloadFragmentData.SetLength(0);
                            }
                        }
                        break;
                    case (byte)TPDU.TPDU_TYPES.DR:
                        // Disconnect Request
                        client.client.EndConnection();
                        break;
                    default:
                        Log("Cannot handle pdu type " + PDU.PDUType);
                        break;
                }
                // Build next packet to work on
                workedLen += PKT.Length;
                Array.Copy(inPacket, workedLen, packet, 0, len - workedLen);
            }
        }

        public int Send(ConnectionState state, byte[] data, int len)
        {
            byte[] d = new byte[len];
            Array.Copy(data, d, len);
            return Send(state, d);
        }

        public int Send(ConnectionState state, byte[] data)
        {
            if (!Connected)
            {
                Log("Cannot send in state 'not connected'");
                return -1;
            }
            TPKT resPkt = new TPKT();
            TPDU resPdu = new TPDU();
            if (data.Length <= m_MaxTPDUSize)
            {
                // No fragmentation
                resPdu.MakeDataPacket(data);
                resPkt.SetPayload(resPdu.GetBytes());
                TCPSend(state, resPkt.GetBytes());
            } 
            else
            {
                // With fragmentation
                int workedLength = 0;
                int fragLength;
                bool lastDataUnit;
                while (workedLength < data.Length)
                {
                    fragLength = data.Length - workedLength;
                    if (fragLength > m_MaxTPDUSize)
                    {
                        fragLength = m_MaxTPDUSize;
                        lastDataUnit = false;
                    }
                    else
                    {
                        lastDataUnit = true;
                    }
                    resPdu.MakeDataPacket(data, workedLength, fragLength, lastDataUnit);
                    resPkt.SetPayload(resPdu.GetBytes());
                    TCPSend(state, resPkt.GetBytes());
                    workedLength += fragLength;
                }
            }
            return 0;
        }
    }
}
