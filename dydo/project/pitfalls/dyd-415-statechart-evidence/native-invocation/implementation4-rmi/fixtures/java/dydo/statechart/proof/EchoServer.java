/* Copyright (c) 2026 Balazs Bodnar. SPDX-License-Identifier: MIT */
package dydo.statechart.proof;

import java.rmi.RemoteException;
import java.rmi.server.UnicastRemoteObject;

public final class EchoServer extends UnicastRemoteObject implements Echo {
    private static final long serialVersionUID = 1L;

    public EchoServer() throws RemoteException {
        super();
    }

    @Override
    public String echo(String value) {
        return value;
    }
}
