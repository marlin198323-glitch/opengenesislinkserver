// SPDX-License-Identifier: MPL-2.0
using System;
using OpenSim.Server.Handlers.Inventory;

internal static class Program
{
    private static int checks;
    private static void Check(string method, bool shouldAllow)
    {
        bool allowed = XInventoryConnectorPostHandler.IsAllowedPublicHypergridMethod(method);
        if (allowed != shouldAllow)
            throw new InvalidOperationException("HG inventory method authorization mismatch: " + method);
        checks++;
    }

    private static int Main()
    {
        try
        {
            // These reads preserve visibility of the HG travel suitcase.
            foreach (string method in new[] {
                "GETINVENTORYSKELETON", "GETROOTFOLDER", "GETFOLDERFORTYPE",
                "GETFOLDERCONTENT", "GETMULTIPLEFOLDERSCONTENT",
                "GETFOLDERITEMS", "GETITEM", "GETMULTIPLEITEMS",
                "GETFOLDER", "GETACTIVEGESTURES", "GETASSETPERMISSIONS"
            })
                Check(method, true);

            // No public write may be reachable with caller-controlled PRINCIPAL.
            foreach (string method in new[] {
                "CREATEUSERINVENTORY", "ADDFOLDER", "UPDATEFOLDER",
                "MOVEFOLDER", "DELETEFOLDERS", "PURGEFOLDER", "ADDITEM",
                "UPDATEITEM", "MOVEITEMS", "DELETEITEMS", "",
                "unknown", "GETITEMS", "getitem", "GETITEM;ADDITEM"
            })
                Check(method, false);

            Console.WriteLine("HG inventory public dispatch safety OK: " + checks + " checks");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
