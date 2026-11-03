/* Copyright (c) 2026 Balazs Bodnar. SPDX-License-Identifier: MIT */
package dydo.statechart.proof;

import java.rmi.Remote;
import java.rmi.RemoteException;

public interface Echo extends Remote {
    String echo(String value) throws RemoteException;
}
