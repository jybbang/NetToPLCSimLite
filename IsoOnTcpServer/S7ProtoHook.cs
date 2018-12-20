﻿/*********************************************************************
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

namespace IsoOnTcp
{
    /// <summary>
    /// This class implements parts of the S7 protocol.
    /// For special purposes not all requests are forwarded to the Plcsim interface.
    /// One special case is a SZL request which asks if we support cyclic data.
    /// Plcsim answers thats it's possible, but the handling of the s7online interface
    /// can't handle this, respectively I can't handle the interface because I don't know
    /// how to use it that it can handle telegrams which are initiates by plcsim.
    /// </summary>
    static class S7ProtoHook
    {
        static public byte[] RequestExchange(byte[] pdubytes)
        {
            // At this time the only thing I want to handle is a SZL-Request of SZL ID 0x0131 Index 3 and
            // make my own response to it.
            // So do it the simple way...
            bool header_ok, parameter_ok, data_ok;
            short SZL_id, SZL_index;
            // 1. Check length: A SZL request is 26 bytes long
            if (pdubytes.Length == 26)
            {
                // 2. Check Header
                header_ok = (pdubytes[0] == 0x32 && pdubytes[1] == 0x07 &&      // Protocol ID, Userdata
                        pdubytes[6] == 0x00 && pdubytes[7] == 0x08 &&           // Length of Parameter is 8 bytes
                        pdubytes[8] == 0x00 && pdubytes[9] == 0x08);            // Length of data is 8 bytes

                if (header_ok)
                {
                    // 3. Check parameter
                    parameter_ok = (pdubytes[10] == 0x00 && pdubytes[11] == 0x01 && pdubytes[12] == 0x12 &&
                        pdubytes[13] == 0x04 && pdubytes[14] == 0x11 && pdubytes[15] == 0x44 &&
                        pdubytes[16] == 0x01 && pdubytes[17] == 0x00);

                    if (parameter_ok)
                    {
                        // 4. Check data
                        data_ok = (pdubytes[18] == 0xff && pdubytes[19] == 0x09 &&
                            pdubytes[20] == 0x00 && pdubytes[21] == 0x04);

                        if (data_ok)
                        {
                            SZL_id = ByteConvert.GetInt16at(pdubytes, 22);
                            SZL_index = ByteConvert.GetInt16at(pdubytes, 24);

                            /* 4.11.2015: As we now support cyclic telegrams,
                             * it is no longer neccessary to build answers for ID 0x0131.
                             * LED-State SZL still not supported by Plcsim.
                             */

                            //System.Diagnostics.Debug.Print("SZL-Request ID " + SZL_id + " Index " + SZL_index);
                            //if (SZL_id == 0x0131 && SZL_index == 0x0003)
                            //{
                            //    return GetSzlId0131_idx3_response(ref pdubytes);
                            if (SZL_id == 0x0074 && SZL_index == 0x0000)
                            {
                                return GetSzlId0x0074_response(ref pdubytes);
                            }
                            else if (SZL_id == 0x0f74 && SZL_index == 0x0000)
                            {
                                return GetSzlId0x0f74_idx0_response(ref pdubytes);
                            }
                            else if (SZL_id == 0x0174)
                            {
                                return GetSzlId0x0174_response(SZL_index, ref pdubytes);
                            }
                            /*
                            else if (SZL_id == 0x0011)
                            {
                                return GetSzlId0x0011_response(ref pdubytes);
                            }
                            else if (SZL_id == 0x001c)
                            {
                                return GetSzlId0x001c_response(ref pdubytes);
                            }
                            * */
                        }
                    }
                }
            }
            return null;
        }

        // Get LED states (are faked, RUN LED is always on
        static public byte[] GetSzlId0x0074_response(ref byte[] pdubytes)
        {
            byte[] szl_id0074_idx0_response = new byte[]
            {
                // Header
                0x32, 0x07, 0x00, 0x00,
                0x00, 0x00,             // PDU reference
                0x00, 0x0c,             // Parameter length
                0x00, 0x24,             // Data length
                // Parameter
                0x00, 0x01, 0x12,
                0x08, 0x12, 0x84, 0x01,
                0x00,                   // Sequence number, should work with zero
                0x00, 0x00, 0x00, 0x00,
                // Data head
                0xff,
                0x09,
                0x00, 0x20,             // 32 bytes following
                //////////////////////////////////////
                0x00, 0x74, 0x00, 0x00, // SZL ID 0x0074 Index 0
                0x00, 0x04, 0x00, 0x06, // 6 element of 4 bytes
                0x00, 0x01, 0x00, 0x00, // SF LED off
                0x00, 0x04, 0x01, 0x00, // RUN LED on
                0x00, 0x05, 0x00, 0x00, // STOP LED off
                0x00, 0x06, 0x00, 0x00, // FRCE LED off
                0x00, 0x0b, 0x00, 0x00, // BUS1F LED off
                0x00, 0x0c, 0x00, 0x00  // BUS2F LED off
            };
            // insert PDU reference id from request
            szl_id0074_idx0_response[4] = pdubytes[4];
            szl_id0074_idx0_response[5] = pdubytes[5];
            return szl_id0074_idx0_response;
        }

        // Get only header informations
        static public byte[] GetSzlId0x0f74_idx0_response(ref byte[] pdubytes)
        {
            byte[] szl_id0f74_idx0_response = new byte[]
            {
                // Header
                0x32, 0x07, 0x00, 0x00,
                0x00, 0x00,             // PDU reference
                0x00, 0x0c,             // Parameter length
                0x00, 0x0c,             // Data length
                // Parameter
                0x00, 0x01, 0x12,
                0x08, 0x12, 0x84, 0x01,
                0x00,                   // Sequence number, should work with zero
                0x00, 0x00, 0x00, 0x00,
                // Data head
                0xff,
                0x09,
                0x00, 0x08,             // 8 bytes following
                //////////////////////////////////////
                0x0f, 0x74, 0x00, 0x00, // SZL ID 0x0f74 Index 0
                0x00, 0x04, 0x00, 0x06  // 6 elements of 4 bytes
            };
            // insert PDU reference id from request
            szl_id0f74_idx0_response[4] = pdubytes[4];
            szl_id0f74_idx0_response[5] = pdubytes[5];
            return szl_id0f74_idx0_response;
        }

        // Get specific LED state
        static public byte[] GetSzlId0x0174_response(short szl_index, ref byte[] pdubytes)
        {
            byte[] szl_id0174_response = new byte[]
            {
                // Header
                0x32, 0x07, 0x00, 0x00,
                0x00, 0x00,             // PDU reference
                0x00, 0x0c,             // Parameter length
                0x00, 0x10,             // Data length
                // Parameter
                0x00, 0x01, 0x12,
                0x08, 0x12, 0x84, 0x01,
                0x00,                   // Sequence number, should work with zero
                0x00, 0x00, 0x00, 0x00,
                // Data head
                0xff,
                0x09,
                0x00, 0x0c,             // 12 bytes following
                //////////////////////////////////////
                0x01, 0x74, 0x00, 0x00, // SZL ID 0x0074 Index XY
                0x00, 0x04, 0x00, 0x01, // 1 element of 4 bytes
                0x00, 0x00, 0x00, 0x00, // Requested LED off
            };
            // insert PDU reference id from request
            szl_id0174_response[4] = pdubytes[4];
            szl_id0174_response[5] = pdubytes[5];
            // insert requested Index
            szl_id0174_response[28] = pdubytes[24];
            szl_id0174_response[29] = pdubytes[25];
            szl_id0174_response[34] = pdubytes[24];
            szl_id0174_response[35] = pdubytes[25];
            // Answer only the LEDs as answered in the other requested,
            // otherwise pass it to PLCSIM
            if (szl_index == 0x01 || szl_index == 0x04 || szl_index == 0x05 ||
                szl_index == 0x06 || szl_index == 0x0b || szl_index == 0x0c) {

                // set only RUN LED on
                if (szl_index == 4)
                {
                    szl_id0174_response[36] = 0x01;
                }
                return szl_id0174_response;
            }
            return null;
        }

        static public byte[] GetSzlId0131_idx3_response(ref byte[] pdubytes)
        {
            byte[] szl_id0131_idx3_response = new byte[]
            {
                // Header
                0x32, 0x07, 0x00, 0x00,
                0x00, 0x00,             // PDU reference
                0x00, 0x0c,             // Parameter length
                0x00, 0x34,             // Data length
                // Parameter
                0x00, 0x01, 0x12,
                0x08, 0x12, 0x84, 0x01,
                0x00,                   // Sequence number, should work with zero
                0x00, 0x00, 0x00, 0x00,
                // Data head
                0xff,
                0x09,
                0x00, 0x30,             // 48 bytes following
                //////////////////////////////////////
                0x01, 0x31, 0x00, 0x03,	// SZL ID 0x131 Index 3
                0x00, 40, 0x00, 0x01,   // 1 element of 40 bytes
                0x00, 0x03, 	        // index
                0x00,		            // funkt_0: changed from 0x7f to 0x00 (no cyclic functions available)
                0xf0,		            // funkt_1
                0x83,		            // funkt_2
                0x01,		            // funkt_3
                0x00, 0x00,	            // data, (maximum size of concistently readable data
                0x00, 0x00,	            // anz (maximum number of cyclic read jobs
                0x00, 0x01,	            // per_min
                0x02, 0x09,	            // per_man
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00, // res
            };
            // insert PDU reference id from request
            szl_id0131_idx3_response[4] = pdubytes[4];
            szl_id0131_idx3_response[5] = pdubytes[5];
            return szl_id0131_idx3_response;
        }

        static public byte[] GetSzlId0x0011_response(ref byte[] pdubytes)
        {
            byte[] szl_id0011_response = new byte[]
            {
                // Header
                0x32, 0x07, 0x00, 0x00,
                0x00, 0x00,             // PDU reference
                0x00, 0x0c,             // Parameter length
                0x00, 0x7c,             // Data length
                // Parameter
                0x00, 0x01, 0x12,
                0x08, 0x12, 0x84, 0x01,
                0x00,                   // Sequence number, should work with zero
                0x00, 0x00, 0x00, 0x00,
                // Data head
                //0xff,
                //0x09,
                //0x00, 0x78,             // bytes following
                //////////////////////////////////////
                // 6ES7 151-8AB01-0AB0
                0xff, 0x09, 0x00, 0x78, 0x00, 0x11, 0x00, 0x00, 0x00, 0x1c, 0x00, 0x04, 0x00, 0x01, 0x36, 0x45,
                0x53, 0x37, 0x20, 0x31, 0x35, 0x31, 0x2d, 0x38, 0x41, 0x42, 0x30, 0x31, 0x2d, 0x30, 0x41, 0x42,
                0x30, 0x20, 0x00, 0xc0, 0x00, 0x03, 0x00, 0x01, 0x00, 0x06, 0x36, 0x45, 0x53, 0x37, 0x20, 0x31,
                0x35, 0x31, 0x2d, 0x38, 0x41, 0x42, 0x30, 0x31, 0x2d, 0x30, 0x41, 0x42, 0x30, 0x20, 0x00, 0xc0,
                0x00, 0x03, 0x00, 0x01, 0x00, 0x07, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0xc0, 0x56, 0x03, 0x02, 0x06,
                0x00, 0x81, 0x42, 0x6f, 0x6f, 0x74, 0x20, 0x4c, 0x6f, 0x61, 0x64, 0x65, 0x72, 0x20, 0x20, 0x20,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0x00, 0x41, 0x20, 0x09, 0x09
            };
            // insert PDU reference id from request
            szl_id0011_response[4] = pdubytes[4];
            szl_id0011_response[5] = pdubytes[5];
            // insert requested Index
            szl_id0011_response[28] = pdubytes[24];
            szl_id0011_response[29] = pdubytes[25];
            szl_id0011_response[34] = pdubytes[24];
            szl_id0011_response[35] = pdubytes[25];

            return szl_id0011_response;
        }

        static public byte[] GetSzlId0x001c_response(ref byte[] pdubytes)
        {
            // Attention! This telegram needs a 480 bytes PDU if used!
            byte[] szl_id001c_response = new byte[]
            {
                // Header
                0x32, 0x07, 0x00, 0x00,
                0x00, 0x00,             // PDU reference
                0x00, 0x0c,             // Parameter length
                0x01, 0x60,             // Data length
                // Parameter
                0x00, 0x01, 0x12,
                0x08, 0x12, 0x84, 0x01,
                0x00,                   // Sequence number, should work with zero
                0x00, 0x00, 0x00, 0x00,
                // Data head
                //0xff,
                //0x09,
                //0x00, 0x78,             // bytes following
                //////////////////////////////////////
                0xff, 0x09, 0x01, 0x5c, 0x00, 0x1c, 0x00, 0x00, 0x00, 0x22, 0x00, 0x0a, 0x00, 0x01, 0x49, 0x4d,
                0x31, 0x35, 0x31, 0x2d, 0x38, 0x2d, 0x43, 0x50, 0x55, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02,
                0x49, 0x4d, 0x31, 0x35, 0x31, 0x2d, 0x38, 0x20, 0x50, 0x4e, 0x2f, 0x44, 0x50, 0x20, 0x43, 0x50,
                0x55, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x04, 0x4f, 0x72, 0x69, 0x67, 0x69, 0x6e, 0x61, 0x6c, 0x20, 0x53, 0x69, 0x65,
                0x6d, 0x65, 0x6e, 0x73, 0x20, 0x45, 0x71, 0x75, 0x69, 0x70, 0x6d, 0x65, 0x6e, 0x74, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x53, 0x20, 0x43, 0x2d, 0x43, 0x36, 0x54, 0x57, 0x37, 0x34,
                0x38, 0x38, 0x32, 0x30, 0x31, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x49, 0x4d, 0x31, 0x35, 0x31, 0x2d, 0x38, 0x20,
                0x50, 0x4e, 0x2f, 0x44, 0x50, 0x20, 0x43, 0x50, 0x55, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08,

                0x4d, 0x4d, 0x43, 0x20, 0x32, 0x39, 0x30, 0x30, 0x46, 0x43, 0x31, 0x41, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x09, 0x00, 0x2a, 0xf6, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x0a, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };
            // insert PDU reference id from request
            szl_id001c_response[4] = pdubytes[4];
            szl_id001c_response[5] = pdubytes[5];
            // insert requested Index
            szl_id001c_response[28] = pdubytes[24];
            szl_id001c_response[29] = pdubytes[25];
            szl_id001c_response[34] = pdubytes[24];
            szl_id001c_response[35] = pdubytes[25];

            return szl_id001c_response;
        }

        static public void ResponseExchange(ref byte[] pdubytes)
        {
            // Look into PLCSIM response for data we have to modify.
            // At this time this is the response to communication setup response, where PLCSIM answers
            // with more than one parallel job.
            // At this time nettoplcsim can only handle one job at time, so this value has to be set to 1.
            bool header_ok, parameter_ok;
            // 1. Check length: A Response to communication setup is 20 bytes long.
            if (pdubytes.Length == 20)
            {
                // 2. Check Header
                header_ok = (pdubytes[0] == 0x32 && pdubytes[1] == 0x03 &&      // Protocol ID, Ackdata
                        pdubytes[6] == 0x00 && pdubytes[7] == 0x08 &&           // Length of Parameter is 8 bytes
                        pdubytes[8] == 0x00 && pdubytes[9] == 0x00);            // Length of data is 0 bytes

                if (header_ok)
                {
                    // 3. Check parameter
                    parameter_ok = (pdubytes[12] == 0xf0);

                    if (parameter_ok)
                    {
                        // Set Max AmQ calling to 1
                        pdubytes[14] = 0x00;
                        pdubytes[15] = 0x01;
                        // Set Max AmQ called to 1
                        pdubytes[16] = 0x00;
                        pdubytes[17] = 0x01;
                    }
                }
            }
        }
    }
}
